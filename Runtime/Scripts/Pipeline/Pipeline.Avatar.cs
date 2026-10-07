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
                    DuplicateMergeAnimators();
                }
            }
            using (timings.Measure("animations"))
                AddKeyAnimations(clone);
            using (timings.Measure("blendshapes"))
                ObfuscateBlendShapes(clone);
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
                    if (EmissionEncryption.IsEmissionMap(mat, name))
                        continue;
                    if (!(mat.GetTexture(name) is Texture2D mainTexture) || !processedTextures.TryGetValue(mainTexture, out ProcessedTexture processed))
                        continue;

                    Texture2D encrypted0 = processed.Encrypted.Texture1;
                    Texture2D bigFallbackTexture = GetLargestFallback(processed);

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

            var duplicatedMaterials = new Dictionary<Material, Material>();
            ReplaceProcessedTexturesWithFallbacks(Avatar.GetComponentsInChildren<MeshRenderer>(true), duplicatedMaterials);
            ReplaceProcessedTexturesWithFallbacks(Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true), duplicatedMaterials);
        }

        void ReplaceProcessedTexturesWithFallbacks<T>(IEnumerable<T> renderers, Dictionary<Material, Material> duplicatedMaterials) where T : Renderer
        {
            foreach (T renderer in renderers)
            {
                Material[] mats = renderer.sharedMaterials;
                if (mats == null)
                    continue;

                bool rendererChanged = false;
                for (int i = 0; i < mats.Length; ++i)
                {
                    Material sourceMaterial = mats[i];
                    if (sourceMaterial == null)
                        continue;

                    Material duplicatedMaterial = null;
                    foreach (string name in sourceMaterial.GetTexturePropertyNames())
                    {
                        // Emission is opt-in, even if an unselected slot shares
                        // a texture encrypted as another material's main map.
                        if (EmissionEncryption.IsEmissionMap(sourceMaterial, name)) continue;
                        Texture2D texture = sourceMaterial.GetTexture(name) as Texture2D;
                        if (texture == null || !Result.processedTextures.TryGetValue(texture, out ProcessedTexture processedTexture))
                            continue;

                        if (duplicatedMaterial == null)
                            duplicatedMaterial = GetOrCreateDuplicatedMaterial(sourceMaterial, duplicatedMaterials);

                        duplicatedMaterial.SetTexture(name, GetLargestFallback(processedTexture));
                        EditorUtility.SetDirty(duplicatedMaterial);
                    }

                    if (duplicatedMaterial != null)
                    {
                        mats[i] = duplicatedMaterial;
                        rendererChanged = true;
                    }
                }

                if (rendererChanged)
                    renderer.sharedMaterials = mats;
            }
        }

        Material GetOrCreateDuplicatedMaterial(Material source, Dictionary<Material, Material> duplicatedMaterials)
        {
            if (duplicatedMaterials.TryGetValue(source, out Material duplicatedMaterial))
                return duplicatedMaterial;

            // Always a new copy: a file with this name can be an earlier build's copy, or the copy of another material
            // with the same name, and its other properties would be wrong.
            duplicatedMaterial = Object.Instantiate(source);
            writer.CreateAssetInFolder(duplicatedMaterial, paths.Folders.MatGuid, paths.DuplicatedMaterialName(source));

            duplicatedMaterials[source] = duplicatedMaterial;
            return duplicatedMaterial;
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

        // The copy must not change the original's merged animators, so it gets its own.
        void DuplicateMergeAnimators()
        {
#if MODULAR
            foreach (var maMergeAnim in Avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
                maMergeAnim.animator = AnimatorManager.DuplicateAnimator(maMergeAnim.animator, paths, writer);
#endif
        }

        void AddKeyAnimations(bool clone)
        {
            var av3 = Avatar.GetComponent<VRCAvatarDescriptor>();
            AnimatorController fx;
            if (clone)
            {
                var sourceControllers = new RuntimeAnimatorController[av3.baseAnimationLayers.Length];
                for (int i = 0; i < av3.baseAnimationLayers.Length; ++i)
                    sourceControllers[i] = av3.baseAnimationLayers[i].animatorController;

                AnimatorController[] duplicatedLayers = AnimatorManager.DuplicateAnimators(sourceControllers, paths, writer);
                for (int i = 0; i < duplicatedLayers.Length; ++i)
                {
                    if (duplicatedLayers[i] != null)
                        av3.baseAnimationLayers[i].animatorController = duplicatedLayers[i];
                }

                fx = duplicatedLayers[4];
            }
            else
                fx = av3.baseAnimationLayers[4].animatorController as AnimatorController;

            string animationDir = writer.ResolveFolderPath(paths.Folders.AnimGuid);
            AnimatorManager.CreateKeyAnimations(OutputPaths.Combine(settings.RuntimeDir, "Animations"), paths, writer, Result.meshes.ToArray());
            AnimatorManager.AddKeyLayer(fx, animationDir, settings.KeySize, settings.SyncSize, 3.0f, settings.UserKey);
        }

        public void ObfuscateBlendShapes(bool clone)
        {
            EnsureOutputs();
            var av3 = Avatar.GetComponent<VRCAvatarDescriptor>();

            Obfuscator obfuscator = ScriptableObject.CreateInstance<Obfuscator>();
            obfuscator.Clone = clone;
            obfuscator.PreserveMmd = settings.PreserveMMD;

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

                        GameObject targetObject = binding.ReferenceMesh.Get(maBlendshapeSync);
                        SkinnedMeshRenderer targetRenderer = targetObject.GetComponent<SkinnedMeshRenderer>();
                        SkinnedMeshRenderer syncRenderer = maBlendshapeSync.GetComponent<SkinnedMeshRenderer>();

                        if (targetRenderer == null)
                            continue;
                        if (targetRenderer == selectRenderer)
                        {
                            string obfuscatedShape = obfuscator.GetOriginalBlendShapeName(binding.Blendshape);
                            if (obfuscatedShape != null)
                                binding.Blendshape = obfuscatedShape;
                        }

                        if (syncRenderer == null)
                            continue;
                        if (syncRenderer == selectRenderer)
                        {
                            string obfuscatedShape = obfuscator.GetOriginalBlendShapeName(binding.LocalBlendshape);
                            if (obfuscatedShape != null)
                                binding.LocalBlendshape = obfuscator.GetOriginalBlendShapeName(binding.LocalBlendshape);
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

        void ReplaceMaterialsInAnimations(bool clone)
        {
            var fx = GetFx(Avatar);

            AnimatorManager animManager = ScriptableObject.CreateInstance<AnimatorManager>();
            foreach (var pair in Result.encryptedMaterials)
            {
                Debug.LogFormat("{0}, {1}", pair.Key.name, pair.Value.name);
                animManager.ChangeAnimationMaterial(fx, pair.Key, pair.Value, clone, paths, writer);
            }

#if MODULAR
            if (clone)
            {
                var maMergeAnims = Avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                foreach (var maMergeAnim in maMergeAnims)
                {
                    if (maMergeAnim.animator == null)
                        continue;
                    foreach (var pair in Result.encryptedMaterials)
                    {
                        animManager.ChangeAnimationMaterial(maMergeAnim.animator as AnimatorController, pair.Key, pair.Value, clone, paths, writer);
                    }
                }
            }
#endif
        }
    }
}
#endif
