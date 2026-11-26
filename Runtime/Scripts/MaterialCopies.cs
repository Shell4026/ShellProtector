#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Shell.Protector
{
    // NDMF tools that run before the encryption (Light Limit Changer) put copies of the materials on the avatar. The
    // settings name the originals, so without this a material listed by hand isn't encrypted where the avatar uses it,
    // and the copies lose their options (an inactive material would be encrypted).
    public static class MaterialCopies
    {
        public static void Apply(GameObject avatar, Func<Material, Material> originalOf, BuildSettings settings)
        {
            var copies = new Dictionary<Material, List<Material>>();
            foreach (Material material in AvatarMaterials(avatar))
            {
                Material original = originalOf(material);
                if (original == null || original == material)
                    continue;
                if (!copies.TryGetValue(original, out List<Material> list))
                    copies[original] = list = new List<Material>();
                list.Add(material);
            }

            // The original stays in the list: a material swap that VRCFury adds later still points at it.
            var materials = new List<Material>();
            foreach (Material material in settings.Materials)
            {
                materials.Add(material);
                if (copies.TryGetValue(material, out List<Material> list))
                    materials.AddRange(list);
            }
            settings.Materials = materials.Distinct().ToList();

            foreach (Material material in settings.Materials)
            {
                if (settings.MaterialOptions.ContainsKey(material))
                    continue;
                Material original = originalOf(material);
                if (original != null && original != material && settings.MaterialOptions.TryGetValue(original, out ShellProtector.MatOption option))
                    settings.MaterialOptions[material] = option;
            }
        }

        // Modular Avatar has merged its animators into the playable layers by then.
        static HashSet<Material> AvatarMaterials(GameObject avatar)
        {
            var materials = new HashSet<Material>();
            foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                        materials.Add(material);
                }
            }

            VRCAvatarDescriptor descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
                return materials;
            var layers = (descriptor.baseAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>())
                .Concat(descriptor.specialAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>());
            foreach (AnimationClip clip in layers.Select(l => l.animatorController as AnimatorController).Where(c => c != null).SelectMany(c => c.animationClips).Distinct())
            {
                if (clip == null)
                    continue;
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    {
                        if (key.value is Material material)
                            materials.Add(material);
                    }
                }
            }
            return materials;
        }
    }
}
#endif
