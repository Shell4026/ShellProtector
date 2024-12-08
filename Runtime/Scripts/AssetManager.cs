#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;
using System.Linq;

namespace Shell.Protector
{
    public class AssetManager
    {
        static AssetManager instance;
        readonly Dictionary<string, int> support_version = new Dictionary<string, int>();

        static public AssetManager GetInstance()
        {
            if(instance == null)
                instance = new AssetManager();
            return instance;
        }
        AssetManager()
        {
            support_version.Add("Poiyomi 7.3", 73);
            support_version.Add("Poiyomi 8.0", 80);
            support_version.Add("Poiyomi 8.1", 81);
            support_version.Add("Poiyomi 8.2", 82);
            support_version.Add("Poiyomi 9.0", 90);
            support_version.Add("Poiyomi 9.1", 91);
            support_version.Add("Poiyomi 9.2", 92);
            support_version.Add("lilToon", 0);
        }
        public bool IsPoiyomi(Shader shader)
        {
            if (shader.name.Contains("Poiyomi"))
                return true;
            if (shader.name.Contains("PCSS4Poi"))
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
            int poiyomiLabel = shader.FindPropertyIndex("shader_master_label");
            if (poiyomiLabel != -1)
            {
                var str = shader.GetPropertyDescription(poiyomiLabel);
                if (str.Contains("Poiyomi 9.2"))
                    return support_version["Poiyomi 9.2"];
                else if (str.Contains("Poiyomi 9.1"))
                    return support_version["Poiyomi 9.1"];
                else if (str.Contains("Poiyomi 9.0"))
                    return support_version["Poiyomi 9.0"];
                else if(str.Contains("Poiymoi 8.0"))
                    return support_version["Poiyomi 8.0"];
                else if(str.Contains("Poiyomi 8.1"))
                    return support_version["Poiyomi 8.1"];
                else if (str.Contains("Poiyomi 8.2"))
                    return support_version["Poiyomi 8.2"];
            }
            return -1;
        }

        public bool IsSupportShader(Shader shader)
        {
            if(GetShaderType(shader) == -1)
                return false;
            return true;
        }

        public List<string> CheckShader()
        {
            string[] guids = AssetDatabase.FindAssets("lilConstants");
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string symbols_original = string.Copy(symbols);

            symbols = symbols.Replace(";LILTOON", "");
            symbols = symbols.Replace(";POIYOMI", "");
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

            if (symbols_original.Contains(";LILTOON") != symbols.Contains(";LILTOON"))
            {
                if (symbols_original.Contains(";POIYOMI") != symbols.Contains(";POIYOMI"))
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
                }
            }

            return return_shader;
        }
        public static bool NamespaceExists(string namespaceName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(t => t.GetTypes())
                .Any(t => String.Equals(t.Namespace, namespaceName, StringComparison.Ordinal));
        }

        public void CheckModular()
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string symbols_original = string.Copy(symbols);
            symbols = symbols.Replace(";MODULAR", "");

            if (!NamespaceExists("nadena.dev.ndmf"))
            {
                Debug.Log("ShellProtector: Can't find Modular!");
                if (symbols != symbols_original)
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
                return;
            }
            Debug.Log("ShellProtector: Find Modular!");
            symbols += ";MODULAR";

            if (symbols_original.Contains(";MODULAR") != symbols.Contains(";MODULAR"))
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
            }
        }

        public void ResetDefine()
        {
            Debug.Log("Reset define");
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

            symbols = symbols.Replace(";LILTOON", "");
            symbols = symbols.Replace(";POIYOMI", "");
            symbols = symbols.Replace(";MODULAR", "");

            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
        }
    }
}
#endif