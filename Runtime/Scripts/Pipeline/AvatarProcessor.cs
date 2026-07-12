#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
    internal static class AvatarProcessor
    {
        public static void ReplaceMaterials(GameObject avatar, BuildResult result)
        {
            ReplaceRendererMaterials(avatar.GetComponentsInChildren<MeshRenderer>(true), result);
            ReplaceRendererMaterials(avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true), result);
        }

        static void ReplaceRendererMaterials<T>(IEnumerable<T> renderers, BuildResult result) where T : Renderer
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
                    if (material == null || !result.EncryptedMaterials.ContainsKey(material))
                        continue;

                    materials[i] = result.EncryptedMaterials[material];
                    result.Meshes.Add(renderer.gameObject);
                    changed = true;
                }

                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }
    }
}
#endif
