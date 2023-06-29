using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
    public class ShaderManager
    {
        static ShaderManager instance;
        readonly Dictionary<string, int> support_version = new Dictionary<string, int>();

        static public ShaderManager GetInstance()
        {
            if(instance == null)
                instance = new ShaderManager();
            return instance;
        }
        ShaderManager()
        {
            support_version.Add("Poiyomi 7.3", 7);
            support_version.Add("Poiyomi 8.0", 8);
            support_version.Add("Poiyomi 8.1", 8);
            support_version.Add("Poiyomi 8.2", 8);
            support_version.Add("liltoon", 0);
        }
        public static bool IsPoiyomi(Shader shader)
        {
            if (shader.name.Contains("Poiyomi"))
                return true;
            return false;
        }
        public static bool Isliltoon(Shader shader)
        {
            if (shader.name.Contains("liltoon"))
                return true;
            return false;
        }
        public static bool IsLockPoiyomi(Shader shader)
        {
            if (IsPoiyomi(shader))
            {
                if (shader.name.Contains("Locked"))
                    return true;
                return false;
            }
            return false;
        }
        public int GetSupportShaderType(Shader shader)
        {
            foreach (var version in support_version)
            {
                if (shader.name.Contains(version.Key))
                    return support_version[version.Key];
            }
            return -1;
        }

        public bool IsSupportShader(Shader shader)
        {
            foreach (var version in support_version)
            {
                if (shader.name.Contains(version.Key))
                    return true;
            }
            return false;
        }


    }
}