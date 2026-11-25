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

        // The encrypted materials are changed in place, because material swaps in animations point at them.
        public void ReplaceProtectedTextures()
        {
            EnsureOutputs();

            foreach (KeyValuePair<Material, Material> pair in Result.encryptedMaterials)
            {
                Material mat = pair.Value;
                bool changed = false;
                foreach (string name in mat.GetTexturePropertyNames())
                {
                    if (!(mat.GetTexture(name) is Texture2D texture))
                        continue;
                    Texture replacement = GetEncryptedMaterialSlot(mat, name, texture, pair.Key.mainTexture);
                    if (replacement == null)
                        continue;
                    mat.SetTexture(name, replacement);
                    changed = true;
                }
                if (changed)
                    EditorUtility.SetDirty(mat);
            }

            ReplaceProcessedTexturesWithFallbacks(Avatar.GetComponentsInChildren<MeshRenderer>(true));
            ReplaceProcessedTexturesWithFallbacks(Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        }

        // null leaves the slot to the copy from GetFallbackMaterial.
        Texture GetEncryptedMaterialSlot(Material mat, string name, Texture2D texture, Texture mainTexture)
        {
            if (!Result.processedTextures.TryGetValue(texture, out ProcessedTexture processed))
            {
                // Nothing reuses an emission map's decrypted color. The selected slot already holds black.
                return Result.encryptedEmissionMaps.Contains(texture) ? EncryptionFallback : null;
            }

            // An unselected emission slot. A selected one already holds black.
            if (EmissionEncryption.IsEmissionMap(mat, name))
                return GetLargestFallback(processed);

            bool poiyomi = shaderManager.IsPoiyomi(mat.shader);
            bool lilToon = shaderManager.IsLilToon(mat.shader);
            bool isMain = texture == mainTexture;
            // The injected shader draws the rim with the decrypted main color, so a rim slot holding the main texture takes
            // the material's encrypted texture. Any other protected texture there gets the encryption fallback.
            if (poiyomi && (name == "_RimTex" || name == "_Rim2Tex") || lilToon && name == "_RimColorTex")
                return isMain ? mat.GetTexture(ShaderProperties.EncryptTexture0) : EncryptionFallback;
            if (lilToon && name == "_RimShadeMask")
                return isMain ? GetLargestFallback(processed) : EncryptionFallback;
            if (poiyomi && name == "_OutlineTexture" || lilToon && name == "_OutlineTex")
                return GetLargestFallback(processed);
            return null;
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

        Material GetFallbackMaterial(Material source)
        {
            if (Result.fallbackMaterials.TryGetValue(source, out Material copy))
                return copy;

            foreach (string name in source.GetTexturePropertyNames())
            {
                if (!(source.GetTexture(name) is Texture2D texture))
                    continue;
                Texture2D fallback;
                if (Result.processedTextures.TryGetValue(texture, out ProcessedTexture processedTexture))
                    fallback = GetLargestFallback(processedTexture);
                else if (Result.encryptedEmissionMaps.Contains(texture))
                    fallback = EncryptionFallback;
                else
                    continue;

                if (copy == null)
                {
                    // Always a new copy: a file with this name can be an earlier build's copy, or the copy of another material
                    // with the same name, and its other properties would be wrong.
                    copy = Object.Instantiate(source);
                    writer.CreateAssetInFolder(copy, paths.Folders.MatGuid, paths.DuplicatedMaterialName(source));
                }
                copy.SetTexture(name, fallback);
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

        // Runs before anything changes, so the copy never edits the original's controllers.
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

        // Runs after the tools that add material swaps (VRCFury) or patch blendshapes by their original names.
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
            var obfuscatedRenderers = new HashSet<SkinnedMeshRenderer>();

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
                bool faceTracking = Obfuscator.IsFaceTrackingMesh(av3, selectRenderer);
                Mesh newMesh = obfuscator.ObfuscateBlendShapeMesh(mesh, paths, writer, faceTracking);
                selectRenderer.sharedMesh = newMesh;
                obfuscatedRenderers.Add(selectRenderer);

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
            obfuscator.ChangeFaceTrackingNamesInDescriptor(av3, obfuscatedRenderers);
        }

        // With copyClips a clip is copied before it changes: the manual copy shares the original's clips, and the deferred
        // pass also sees the user's own clips that VRCFury puts back.
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

        Material ResolveAnimatedMaterial(Material material)
        {
            if (Result.encryptedMaterials.TryGetValue(material, out Material encrypted))
                material = encrypted;
            return GetFallbackMaterial(material) ?? material;
        }
    }
}
#endif
