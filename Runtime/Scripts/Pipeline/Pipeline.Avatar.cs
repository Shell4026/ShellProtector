#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

#if MODULAR
using nadena.dev.modular_avatar.core;
#endif

namespace Shell.Protector
{
    // After the materials: point the avatar at the encrypted materials and add what drives the key. The same steps run for
    // manual encryption, which rewrites a copy and duplicates the assets it changes (clone), and for the NDMF pass, which
    // rewrites the build's avatar in place.
    public sealed partial class Pipeline
    {
        void ProcessAvatar()
        {
            bool clone = request.Clone;
            using (timings.Measure("avatar"))
            {
                ReplaceMaterials();
                ReplaceProtectedTextures();
                if (clone)
                {
                    request.Avatar.SetActive(false);
                    AddTester();
                }
            }
            using (timings.Measure("animations"))
                AddKeyAnimations();
            if (!request.DeferObfuscation)
            {
                using (timings.Measure("blendshapes"))
                    ObfuscateBlendShapes(clone);
            }
            using (timings.Measure("animations"))
                ReplaceMaterialsInAnimations(clone);

            Object.DestroyImmediate(Avatar.GetComponentInChildren<ShellProtector>(true));
        }

        public static AnimatorController GetFx(GameObject avatar, int playableLayer = 4)
        {
            var av3 = avatar.GetComponent<VRCAvatarDescriptor>();
            if (av3 == null)
                return null;
            return av3.baseAnimationLayers[playableLayer].animatorController as AnimatorController;
        }

        void ReplaceMaterials()
        {
            ReplaceRendererMaterials(Avatar.GetComponentsInChildren<MeshRenderer>(true));
            ReplaceRendererMaterials(Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        }

        void ReplaceRendererMaterials<T>(IEnumerable<T> renderers) where T : Renderer
        {
            foreach (T renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials == null)
                    continue;

                bool changed = false;
                for (int i = 0; i < materials.Length; ++i)
                {
                    Material material = materials[i];
                    if (material == null || !Result.encryptedMaterials.TryGetValue(material, out Material encrypted))
                        continue;

                    materials[i] = encrypted;
                    Result.meshes.Add(renderer.gameObject);
                    changed = true;
                }

                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        // Protected textures must not stay readable elsewhere on the avatar: the encrypted materials' other slots take the
        // encrypted texture or a fallback, and every other material gets a copy that uses the largest fallback.
        public void ReplaceProtectedTextures()
        {
            EnsureOutputs();
            Dictionary<Texture2D, ProcessedTexture> processedTextures = Result.processedTextures;

            foreach (var mat in Result.encryptedMaterials.Values)
            {
                AuxiliaryTextures otherTex = GetAuxiliaryTextures(mat);
                bool poiyomi = shaderManager.IsPoiyomi(mat.shader);
                bool lilToon = shaderManager.IsLilToon(mat.shader);

                foreach (var name in mat.GetTexturePropertyNames())
                {
                    if (!(mat.GetTexture(name) is Texture2D mainTexture) || !processedTextures.TryGetValue(mainTexture, out ProcessedTexture processed))
                        continue;

                    Texture2D encrypted0 = processed.Encrypted.Texture1;
                    Texture2D bigFallbackTexture = GetLargestFallback(processed);

                    // A selected emission slot already holds black (EmissionEncryption.Apply). An unselected one keeps its map
                    // unless the map is protected. This changes the encrypted material itself, not a copy: material swaps
                    // in animations point at it.
                    if (EmissionEncryption.IsEmissionMap(mat, name))
                    {
                        mat.SetTexture(name, bigFallbackTexture);
                        EditorUtility.SetDirty(mat);
                        continue;
                    }

                    if (otherTex.LimTexture != null)
                    {
                        string texName = poiyomi ? "_RimTex" : lilToon ? "_RimColorTex" : "";
                        if (mainTexture == otherTex.LimTexture)
                            mat.SetTexture(texName, encrypted0);
                        else if (processedTextures.ContainsKey(otherTex.LimTexture))
                            mat.SetTexture(texName, null);
                    }
                    if (otherTex.LimTexture2 != null) // Poiyomi only
                    {
                        string texName = poiyomi ? "_Rim2Tex" : "";
                        if (mainTexture == otherTex.LimTexture2)
                            mat.SetTexture(texName, encrypted0);
                        else if (processedTextures.ContainsKey(otherTex.LimTexture2))
                            mat.SetTexture(texName, null);
                    }
                    if (otherTex.OutlineTexture != null)
                    {
                        string texName = poiyomi ? "_OutlineTexture" : lilToon ? "_OutlineTex" : "";
                        if (mainTexture == otherTex.OutlineTexture)
                            mat.SetTexture(texName, bigFallbackTexture);
                        else if (processedTextures.TryGetValue(otherTex.OutlineTexture, out ProcessedTexture outline))
                            mat.SetTexture(texName, outline.Fallbacks[0]);
                    }
                    if (otherTex.LimShadeTexture != null) // lilToon only
                    {
                        const string texName = "_RimShadeMask";
                        if (mainTexture == otherTex.LimShadeTexture)
                            mat.SetTexture(texName, bigFallbackTexture);
                        else if (processedTextures.ContainsKey(otherTex.LimShadeTexture))
                            mat.SetTexture(texName, null);
                    }
                }
            }

            ReplaceProcessedTexturesWithFallbacks(Avatar.GetComponentsInChildren<MeshRenderer>(true));
            ReplaceProcessedTexturesWithFallbacks(Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        }

        void ReplaceProcessedTexturesWithFallbacks<T>(IEnumerable<T> renderers) where T : Renderer
        {
            foreach (T renderer in renderers)
            {
                Material[] mats = renderer.sharedMaterials;
                if (mats == null)
                    continue;

                bool rendererChanged = false;
                for (int i = 0; i < mats.Length; ++i)
                {
                    Material fallbackMaterial = mats[i] != null ? GetFallbackMaterial(mats[i]) : null;
                    if (fallbackMaterial == null)
                        continue;
                    mats[i] = fallbackMaterial;
                    rendererChanged = true;
                }

                if (rendererChanged)
                    renderer.sharedMaterials = mats;
            }
        }

        // The copy of a material with the largest fallback in place of each protected texture, or null if it uses none.
        // Renderers and material swaps share the copies (BuildResult.fallbackMaterials).
        Material GetFallbackMaterial(Material source)
        {
            if (Result.fallbackMaterials.TryGetValue(source, out Material copy))
                return copy;

            foreach (string name in source.GetTexturePropertyNames())
            {
                if (!(source.GetTexture(name) is Texture2D texture) || !Result.processedTextures.TryGetValue(texture, out ProcessedTexture processedTexture))
                    continue;

                if (copy == null)
                {
                    // Always a new copy: a file with this name can be an earlier build's copy, or the copy of another material
                    // with the same name, and its other properties would be wrong.
                    copy = Object.Instantiate(source);
                    writer.CreateAssetInFolder(copy, paths.Folders.MatGuid, paths.DuplicatedMaterialName(source));
                }
                copy.SetTexture(name, GetLargestFallback(processedTexture));
            }

            if (copy != null)
                EditorUtility.SetDirty(copy);
            Result.fallbackMaterials[source] = copy;
            return copy;
        }

        static Texture2D GetLargestFallback(ProcessedTexture processedTexture)
        {
            int idx = processedTexture.FallbackOptions.IndexOf(processedTexture.FallbackOptions.Max());
            return processedTexture.Fallbacks[idx];
        }

        void AddTester()
        {
            GameObject protectorObject = Avatar.GetComponentInChildren<ShellProtector>(true).gameObject;
            var tester = protectorObject.AddComponent<ShellProtectorTester>();
            tester.Language = settings.Language;
            tester.LanguageIndex = settings.LanguageIndex;
            tester.Protector = request.Owner;
            tester.UserKeyLength = settings.KeySize;
        }

        // The copy must not change the original's animators, so it gets its own copy of every controller the build edits:
        // the playable layers and the merged animators. This runs before anything else is changed, and fails if a
        // controller can't be copied.
        bool DuplicateAnimators()
        {
            var av3 = Avatar.GetComponent<VRCAvatarDescriptor>();
            VRCAvatarDescriptor.CustomAnimLayer[] layers = av3.baseAnimationLayers ?? new VRCAvatarDescriptor.CustomAnimLayer[0];
            AnimatorController[] layerCopies = AnimatorManager.DuplicateAnimators(layers.Select(l => l.animatorController).ToArray(), paths, writer);
            if (layerCopies == null)
                return false;

#if MODULAR
            // Copied separately from the layers: a merged animator that is also a layer must not get the key layers.
            var mergeAnimators = Avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
            AnimatorController[] mergeCopies = AnimatorManager.DuplicateAnimators(mergeAnimators.Select(m => m.animator).ToArray(), paths, writer);
            if (mergeCopies == null)
                return false;

            for (int i = 0; i < mergeCopies.Length; ++i)
            {
                if (mergeCopies[i] != null)
                    mergeAnimators[i].animator = mergeCopies[i];
            }
#endif
            for (int i = 0; i < layerCopies.Length; ++i)
            {
                if (layerCopies[i] != null)
                    layers[i].animatorController = layerCopies[i];
            }
            return true;
        }

        void AddKeyAnimations()
        {
            AnimatorController fx = GetFx(Avatar);

            string animationDir = writer.ResolveFolderPath(paths.Folders.AnimGuid);
            AnimatorManager.CreateKeyAnimations(OutputPaths.Combine(settings.RuntimeDir, "Animations"), paths, writer, Result.meshes.ToArray());
            AnimatorManager.AddKeyLayer(fx, animationDir, settings.KeySize, settings.SyncSize, 3.0f, settings.UserKey);
        }

        // The NDMF build's Optimizing pass (DeferredPass), with the result of the encryption that ran in Transforming. It
        // rewrites the material swaps that tools after Transforming added, such as VRCFury's toggles, and obfuscates the
        // blendshapes after the tools that patch them by their original names or order.
        public static void RunDeferredPass(GameObject avatar, BuildSettings settings, BuildResult result)
        {
            var pipeline = new Pipeline(new BuildRequest(avatar, false), settings, result);
            using (pipeline.timings.Measure("animations"))
                pipeline.ReplaceMaterialsInAnimations(true);
            using (pipeline.timings.Measure("blendshapes"))
                pipeline.ObfuscateBlendShapes(false);
            using (pipeline.timings.Measure("save"))
                AssetDatabase.SaveAssets();
            Debug.LogFormat("[ShellProtector] Deferred pass {0}: {1}", avatar.name, pipeline.timings);
        }

        public void ObfuscateBlendShapes(bool clone)
        {
            EnsureOutputs();
            var av3 = Avatar.GetComponent<VRCAvatarDescriptor>();

            Obfuscator obfuscator = ScriptableObject.CreateInstance<Obfuscator>();
            obfuscator.PreserveMmd = settings.PreserveMMD;
            obfuscator.Copies = Result.clipCopies;

            var childRenderers = Avatar.GetComponentsInChildren<SkinnedMeshRenderer>();

#if MODULAR
            //Check localblendshape is empty in MA Blendshape Sync
            var maBlendshapeSyncs = Avatar.GetComponentsInChildren<ModularAvatarBlendshapeSync>(true);
            foreach (var maBlendshapeSync in maBlendshapeSyncs)
            {
                for (int i = 0; i < maBlendshapeSync.Bindings.Count; ++i)
                {
                    var binding = maBlendshapeSync.Bindings[i];
                    if (binding.LocalBlendshape == null || binding.LocalBlendshape == "")
                        binding.LocalBlendshape = string.Copy(binding.Blendshape);

                    maBlendshapeSync.Bindings[i] = binding;
                }
            }
#endif
            foreach (var renderer in settings.ObfuscationRenderers)
            {
                if (renderer == null)
                    continue;
                SkinnedMeshRenderer selectRenderer = childRenderers.FirstOrDefault(r => r.sharedMesh == renderer.sharedMesh);
                if (selectRenderer == null)
                    continue;

                Mesh mesh = selectRenderer.sharedMesh;
                if (mesh == null)
                {
                    Debug.LogErrorFormat("{0} haven't mesh", renderer.transform.name);
                    continue;
                }
                Mesh newMesh = obfuscator.ObfuscateBlendShapeMesh(mesh, paths, writer);
                selectRenderer.sharedMesh = newMesh;

                ////////Change renderer component shape keys////////
                List<float> weights = new List<float>();
                for (int i = 0; i < newMesh.blendShapeCount; ++i)
                {
                    weights.Add(selectRenderer.GetBlendShapeWeight(i));
                    selectRenderer.SetBlendShapeWeight(i, 0.0f);
                }
                var obList = obfuscator.GetObfuscatedBlendShapeIndex();
                for (int i = 0; i < newMesh.blendShapeCount; ++i)
                {
                    selectRenderer.SetBlendShapeWeight(i, weights[obList[i]]);
                }
                /////////////////////////////////
#if MODULAR
                //Change MA Blendshape Sync component
                foreach (var maBlendshapeSync in maBlendshapeSyncs)
                {
                    for (int i = 0; i < maBlendshapeSync.Bindings.Count; ++i)
                    {
                        var binding = maBlendshapeSync.Bindings[i];

                        GameObject targetObject = binding.ReferenceMesh?.Get(maBlendshapeSync);
                        if (targetObject == null)
                            continue;
                        SkinnedMeshRenderer targetRenderer = targetObject.GetComponent<SkinnedMeshRenderer>();
                        SkinnedMeshRenderer syncRenderer = maBlendshapeSync.GetComponent<SkinnedMeshRenderer>();

                        if (targetRenderer != null && targetRenderer == selectRenderer)
                        {
                            string obfuscatedShape = obfuscator.GetOriginalBlendShapeName(binding.Blendshape);
                            if (obfuscatedShape != null)
                                binding.Blendshape = obfuscatedShape;
                        }

                        if (syncRenderer != null && syncRenderer == selectRenderer)
                        {
                            string obfuscatedShape = obfuscator.GetOriginalBlendShapeName(binding.LocalBlendshape);
                            if (obfuscatedShape != null)
                                binding.LocalBlendshape = obfuscatedShape;
                        }

                        maBlendshapeSync.Bindings[i] = binding;
                    }
                }

                if (clone)
                {
                    var maMergeAnims = Avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                    foreach (var maMergeAnim in maMergeAnims)
                    {
                        obfuscator.ObfuscateBlendshapeInAnim(maMergeAnim.animator as AnimatorController, selectRenderer.gameObject, paths, writer);
                    }
                }
#endif
                for (int i = 0; i <= 4; ++i)
                {
                    AnimatorController playableLayer = GetFx(Avatar, i);
                    if (playableLayer == null)
                        continue;
                    obfuscator.ObfuscateBlendshapeInAnim(playableLayer, selectRenderer.gameObject, paths, writer);
                }
                obfuscator.ChangeObfuscatedBlendShapeInDescriptor(av3, selectRenderer);
                obfuscator.Clean();
            }
        }

        // Points the material swaps in the FX (and, for the copy, the merged animators) at what the renderers got: the
        // encrypted material, or the copy with fallbacks of a material that uses a protected texture. With copyClips a clip is
        // copied before it changes: the manual encryption's copy shares the original's clips, and the deferred pass finds
        // clips that later tools added, such as the user's own clips that VRCFury puts back. The NDMF build's first pass
        // changes Modular Avatar's copies in place.
        void ReplaceMaterialsInAnimations(bool copyClips)
        {
            EnsureOutputs();
            if (Result.encryptedMaterials.Count == 0 && Result.processedTextures.Count == 0)
                return;

            var controllers = new List<AnimatorController> { GetFx(Avatar) };
#if MODULAR
            if (request.Clone)
            {
                foreach (var maMergeAnim in Avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
                    controllers.Add(maMergeAnim.animator as AnimatorController);
            }
#endif
            System.Func<AnimationClip, AnimationClip> getWritable = clip => clip;
            if (copyClips)
                getWritable = clip => Result.clipCopies.GetWritable(clip, "_encrypted", paths, writer);
            AnimatorManager.ReplaceMaterials(controllers, ResolveAnimatedMaterial, getWritable);
        }

        // What a material swap must point to, as ReplaceMaterials and ReplaceProtectedTextures set it on the renderers.
        Material ResolveAnimatedMaterial(Material material)
        {
            if (Result.encryptedMaterials.TryGetValue(material, out Material encrypted))
                material = encrypted;
            return GetFallbackMaterial(material) ?? material;
        }
    }
}
#endif
