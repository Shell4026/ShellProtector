#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Reflection;

namespace Shell.Protector
{
    public class AssetManager
    {
        static AssetManager _instance;
        readonly Dictionary<string, int> _supportedVersions = new Dictionary<string, int>();

        public static AssetManager GetInstance()
        {
            if (_instance == null)
                _instance = new AssetManager();
            return _instance;
        }
        AssetManager()
        {
            _supportedVersions.Add("Poiyomi 8.0", 80);
            _supportedVersions.Add("Poiyomi 8.1", 81);
            _supportedVersions.Add("Poiyomi 8.2", 82);
            _supportedVersions.Add("Poiyomi 9.0", 90);
            _supportedVersions.Add("Poiyomi 9.1", 91);
            _supportedVersions.Add("Poiyomi 9.2", 92);
            _supportedVersions.Add("Poiyomi 9.3", 93);
           _supportedVersions.Add("Poiyomi 10.0", 100);
            _supportedVersions.Add("lilToon", 0);
        }
        public bool IsPoiyomi(Shader shader)
        {
            if (shader.name.Contains("Poiyomi"))
                return true;
            if (shader.name.Contains("PCSS4Poi"))
                return true;
            return false;
        }
        public bool IsLilToon(Shader shader)
        {
            if (shader.name.Contains("lilToon"))
                return true;
            return false;
        }
        public bool IsLockPoiyomi(Material mat)
        {
            return mat.shader.name.StartsWith("Hidden/") && mat.GetTag("OriginalShader", false, "") != "";
        }
        public int GetShaderType(Shader shader)
        {
            foreach (var version in _supportedVersions)
            {
                if (shader.name.Contains(version.Key))
                    return _supportedVersions[version.Key];
            }
            int poiyomiLabel = shader.FindPropertyIndex("shader_master_label");
            if (poiyomiLabel != -1)
            {
                var str = shader.GetPropertyDescription(poiyomiLabel);
                if (str.Contains("Poiyomi 10.0"))
                    return _supportedVersions["Poiyomi 10.0"];
                if (str.Contains("Poiyomi 9.3"))
                    return _supportedVersions["Poiyomi 9.3"];
                if (str.Contains("Poiyomi 9.2"))
                    return _supportedVersions["Poiyomi 9.2"];
                if (str.Contains("Poiyomi 9.1"))
                    return _supportedVersions["Poiyomi 9.1"];
                if (str.Contains("Poiyomi 9.0"))
                    return _supportedVersions["Poiyomi 9.0"];
                if(str.Contains("Poiymoi 8.0"))
                    return _supportedVersions["Poiyomi 8.0"];
                if(str.Contains("Poiyomi 8.1"))
                    return _supportedVersions["Poiyomi 8.1"];
                if (str.Contains("Poiyomi 8.2"))
                    return _supportedVersions["Poiyomi 8.2"];
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
            Debug.Log("Checking Shader...");
            string[] guids = AssetDatabase.FindAssets("lilConstants");
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string originalSymbols = string.Copy(symbols);

            symbols = symbols.Replace(";LILTOON", "");
            symbols = symbols.Replace(";POIYOMI91", "");
            symbols = symbols.Replace(";POIYOMI", "");
            List<string> availableShaders = new List<string>();
            if(guids.Length > 0)
            {
                availableShaders.Add("lilToon");
                symbols += ";LILTOON";
            }
            guids = AssetDatabase.FindAssets("ThryEditor");
            if (guids.Length > 0)
            {
                if (ClassExists("Thry.ThryEditor.ShaderOptimizer"))
                {
                    symbols += ";POIYOMI91";
                    availableShaders.Add("Poiyomi9.1>");
                }
                else // < 9.1
                {
                    symbols += ";POIYOMI";
                    availableShaders.Add("Poiyomi");
                }
            }

            if (originalSymbols.Contains(";LILTOON") != symbols.Contains(";LILTOON") ||
                originalSymbols.Contains(";POIYOMI") != symbols.Contains(";POIYOMI"))
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
            }

            return availableShaders;
        }
        public static bool NamespaceExists(string namespaceName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(t => t.GetTypes())
                .Any(t => String.Equals(t.Namespace, namespaceName, StringComparison.Ordinal));
        }
        public static bool ClassExists(string className)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(t => t.GetTypes())
                .Any(t => t.FullName == className);
        }

        public void CheckModular()
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string originalSymbols = string.Copy(symbols);
            symbols = symbols.Replace(";MODULAR", "");

            if (!NamespaceExists("nadena.dev.ndmf"))
            {
                Debug.Log("ShellProtector: Can't find Modular!");
                if (symbols != originalSymbols)
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
                return;
            }
            Debug.Log("ShellProtector: Find Modular!");
            symbols += ";MODULAR";

            if (originalSymbols.Contains(";MODULAR") != symbols.Contains(";MODULAR"))
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

        public bool LockShader(Material mat)
        {
            if (IsLockPoiyomi(mat))
                return true;

            bool isOldOptimizer = false;

            Type optimizer = Type.GetType("Thry.ThryEditor.ShaderOptimizer, ThryAssemblyDefinition");
            if (optimizer == null)
            {
                isOldOptimizer = true;
                optimizer = Type.GetType("Thry.ShaderOptimizer, ThryAssemblyDefinition");
            }
                
            if (optimizer == null)
            {
                Debug.LogError("Not found the ShaderOptimizer!");
                return false;
            }
            if (!isOldOptimizer)
            {
                MethodInfo lockFn = optimizer.GetMethod("LockMaterials");
                if (lockFn == null)
                {
                    Debug.LogError("Not found LockMaterials()");
                    return false;
                }
                object[] param = 
                {
                    new[] { mat }, 0 
                };
                lockFn.Invoke(null, param);
            }
            else
            {
                MethodInfo lockFn = optimizer.GetMethod("SetLockedForAllMaterials");
                if (lockFn == null)
                {
                    Debug.LogError("Not found SetLockedForAllMaterials()");
                    return false;
                }
                object[] param =
                {
                    new[] { mat }, 1, true, false, true, null
                };
                lockFn.Invoke(null, param);
            }
            return true;
        }
    }
}
#endif
