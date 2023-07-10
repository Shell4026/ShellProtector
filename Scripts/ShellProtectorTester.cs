using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shell.Protector
{
    public class ShellProtectorTester : MonoBehaviour
    {
        public string pwd = "pwd";
        public string lang = "eng";
        public int lang_idx = 0;

        private byte[] MakeKeyBytes()
        {
            byte[] pwd_bytes = new byte[4] { 0, 0, 0, 0 };
            byte[] bytes = Encoding.ASCII.GetBytes(pwd);
            for (int i = 0; i < bytes.Length; ++i)
            {
                if (i == pwd_bytes.Length)
                    break;
                pwd_bytes[i] = bytes[i];
            }
            return pwd_bytes;
        }
        public void CheckEncryption()
        {
            byte[] pwd_bytes = MakeKeyBytes();

            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach(var r in renderers)
            {
                var mats = r.sharedMaterials;
                foreach(var mat in mats)
                {
                    if(mat.name.Contains("_encrypted"))
                    {
                        mat.SetInt("_Key0", pwd_bytes[0]);
                        mat.SetInt("_Key1", pwd_bytes[1]);
                        mat.SetInt("_Key2", pwd_bytes[2]);
                        mat.SetInt("_Key3", pwd_bytes[3]);
                    }
                }
            }
            var skinned_renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in skinned_renderers)
            {
                var mats = r.sharedMaterials;
                foreach (var mat in mats)
                {
                    if (mat.name.Contains("_encrypted"))
                    {
                        mat.SetInt("_Key0", pwd_bytes[0]);
                        mat.SetInt("_Key1", pwd_bytes[1]);
                        mat.SetInt("_Key2", pwd_bytes[2]);
                        mat.SetInt("_Key3", pwd_bytes[3]);
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
                foreach (var mat in mats)
                {
                    if (mat.name.Contains("_encrypted"))
                    {
                        mat.SetInt("_Key0", 0);
                        mat.SetInt("_Key1", 0);
                        mat.SetInt("_Key2", 0);
                        mat.SetInt("_Key3", 0);
                    }
                }
            }
            var skinned_renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in skinned_renderers)
            {
                var mats = r.sharedMaterials;
                foreach (var mat in mats)
                {
                    if (mat.name.Contains("_encrypted"))
                    {
                        mat.SetInt("_Key0", 0);
                        mat.SetInt("_Key1", 0);
                        mat.SetInt("_Key2", 0);
                        mat.SetInt("_Key3", 0);
                    }
                }
            }
        }
    }
}
