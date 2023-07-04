#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Player;
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
            support_version.Add("lilToon", 0);
        }
        public bool IsPoiyomi(Shader shader)
        {
            
            if (shader.name.Contains("Poiyomi"))
                return true;
            return false;
        }
        public bool IslilToon(Shader shader)
        {
            if (shader.name.Contains("lilToon"))
                return true;
            return false;
        }
        public bool IsLockPoiyomi(Shader shader)
        {
            if (shader.name.Contains("Locked"))
                return true;
            return false;
        }
        public int GetShaderType(Shader shader)
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

        public List<string> CheckShader()
        {
            string[] guids = AssetDatabase.FindAssets("lilConstants");
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string symbols_original = string.Copy(symbols);

            symbols = symbols.Replace(";LILTOON", ";");
            symbols = symbols.Replace(";POIYOMI", ";");
            List<string> return_shader = new List<string>();
            if(guids.Length > 0)
            {
                return_shader.Add("lilToon");
                symbols += ";LILTOON";
            }
            guids = AssetDatabase.FindAssets("ThryEditor");
            if (guids.Length > 0)
            {
                return_shader.Add("Poiyomi");
                symbols += ";POIYOMI";
            }

            if(symbols_original != symbols)
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);

            return return_shader;
        }
    }
}
#endif