#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shell.Protector
{
    public class ShellProtectorTester : MonoBehaviour
    {
        public string lang = "eng";
        public int lang_idx = 0;
        public int user_key_length = 4;

        public ShellProtector protector;

        public void CheckEncryption()
        {
            byte[] pwd_bytes = protector.GetKeyBytes();

            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach(var r in renderers)
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
                    if (mat.name.Contains("_encrypted"))
                    {
                        for(int i = 0; i < user_key_length; ++i)
                            mat.SetInt("_Key" + i, pwd_bytes[16 - user_key_length + i]);
                    }
                }
            }
            var skinned_renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in skinned_renderers)
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
                    if (mat.name.Contains("_encrypted"))
                    {
                        for (int i = 0; i < user_key_length; ++i)
                            mat.SetInt("_Key" + i, pwd_bytes[16 - user_key_length + i]);
                    }
                }
            }
        }
        public void ResetEncryption()
        {
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
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
                    if (mat.name.Contains("_encrypted"))
                    {
                        for (int i = 0; i < 16; ++i)
                            mat.SetInt("_Key" + i, 0);
                    }
                }
            }
            var skinned_renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in skinned_renderers)
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
                    if (mat.name.Contains("_encrypted"))
                    {
                        for (int i = 0; i < 16; ++i)
                            mat.SetInt("_Key" + i, 0);
                    }
                }
            }
        }
    }
}
#endif