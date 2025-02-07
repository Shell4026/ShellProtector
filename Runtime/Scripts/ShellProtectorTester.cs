﻿#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VRC.SDKBase;

namespace Shell.Protector
{
    public class ShellProtectorTester : MonoBehaviour, IEditorOnly
    {
        public string lang = "eng";
        public int lang_idx = 0;
        public int userKeyLength = 4;

        public ShellProtector protector;

        public void CheckEncryption()
        {
            if(protector == null)
            {
                Debug.LogWarning("First, you need to set protector");
                return;
            }

            ShellProtector.SetMaterialFallbackValue(transform.root.gameObject, false);

            byte[] pwd_bytes = protector.GetKeyBytes();

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
                                mat.SetInt("_Key" + i, pwd_bytes[i]);
                        }
                    }
                }
            }
            var skinned_renderers = transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned_renderers != null)
            {
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
                        if (mat.name.Contains("_encrypted") || mat.name.Contains("_duplicated"))
                        {
                            for (int i = 0; i < 16; ++i)
                                mat.SetInt("_Key" + i, pwd_bytes[i]);
                        }
                    }
                }
            }
        }
        public void ResetEncryption()
        {
            ShellProtector.SetMaterialFallbackValue(transform.root.gameObject, true);

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
                        for (int i = 16 - userKeyLength; i < 16; ++i)
                            mat.SetInt("_Key" + i, 0);
                    }
                }
            }
            var skinned_renderers = transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
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
                    if (mat.name.Contains("_encrypted") || mat.name.Contains("_duplicated"))
                    {
                        for (int i = 16 - userKeyLength; i < 16; ++i)
                            mat.SetInt("_Key" + i, 0);
                    }
                }
            }
        }
    }
}
#endif