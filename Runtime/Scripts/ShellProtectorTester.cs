#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;

namespace Shell.Protector
{
    public class ShellProtectorTester : MonoBehaviour, IEditorOnly
    {
        [FormerlySerializedAs("lang")]
        public string Language = "eng";
        [FormerlySerializedAs("langIdx")]
        public int LanguageIndex;
        [FormerlySerializedAs("userKeyLength")]
        public int UserKeyLength = 4;

        [FormerlySerializedAs("protector")]
        public ShellProtector Protector;

        public void CheckEncryption()
        {
            if (Protector == null)
            {
                Debug.LogWarning("First, you need to set protector");
                return;
            }

            byte[] passwordBytes = Protector.GetKeyBytes();

            var renderers = transform.root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    var mats = r.sharedMaterials;
                    if (mats == null)
                    {
                        Debug.LogWarning(r.gameObject.name + ": can't find sharedMaterials");
                        continue;
                    }
                    foreach (var mat in mats)
                    {
                        if (mat == null)
                            continue;
                        if (mat.name.Contains("_encrypted") || mat.name.Contains("_duplicated"))
                        {
                            for (int i = 0; i < 16; ++i)
                                mat.SetInt(ShaderProperties.KeyPrefix + i, passwordBytes[i]);
                        }
                    }
                }
            }
            var skinnedRenderers = transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinnedRenderers != null)
            {
                foreach (var r in skinnedRenderers)
                {
                    var mats = r.sharedMaterials;
                    if (mats == null)
                    {
                        Debug.LogWarning(r.gameObject.name + ": can't find sharedMaterials");
                        continue;
                    }
                    foreach (var mat in mats)
                    {
                        if (mat == null)
                            continue;
                        if (mat.name.Contains("_encrypted") || mat.name.Contains("_duplicated"))
                        {
                            for (int i = 0; i < 16; ++i)
                                mat.SetInt(ShaderProperties.KeyPrefix + i, passwordBytes[i]);
                        }
                    }
                }
            }
        }
        public void ResetEncryption()
        {
            var renderers = transform.root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                if (mats == null)
                {
                    Debug.LogWarning(r.gameObject.name + ": can't find sharedMaterials");
                    continue;
                }
                foreach (var mat in mats)
                {
                    if (mat == null)
                        continue;
                        if (mat.name.Contains("_encrypted") || mat.name.Contains("_duplicated"))
                        {
                            for (int i = 16 - UserKeyLength; i < 16; ++i)
                                mat.SetInt(ShaderProperties.KeyPrefix + i, 0);
                        }
                }
            }
            var skinnedRenderers = transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in skinnedRenderers)
            {
                var mats = r.sharedMaterials;
                if (mats == null)
                {
                    Debug.LogWarning(r.gameObject.name + ": can't find sharedMaterials");
                    continue;
                }
                foreach (var mat in mats)
                {
                    if (mat == null)
                        continue;
                        if (mat.name.Contains("_encrypted") || mat.name.Contains("_duplicated"))
                        {
                            for (int i = 16 - UserKeyLength; i < 16; ++i)
                                mat.SetInt(ShaderProperties.KeyPrefix + i, 0);
                        }
                }
            }
        }
    }
}
#endif
