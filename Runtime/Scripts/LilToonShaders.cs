#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    // The lilToon shaders for encrypted materials: one copy of Runtime/liltoonProtector/Shaders per project that bakes
    // in the project's ShaderSecrets. Only files that differ from the package's are rewritten, so Unity compiles the copy
    // once per project (and again after a package update) instead of on every upload.
    public static class LilToonShaders
    {
        public const string Folder = "Assets/ShellProtector/lilToon";
        const string SettingsFile = "ShellProtectorSecrets.json";
        const string DataFile = "lilCustomShaderDatas.lilblock";
        const string InsertFile = "custom_insert.hlsl";

        [Serializable]
        class Settings
        {
            // Appended to the shader name, so the copy doesn't clash with the package's shaders or with another copy.
            public string id;
            public ShaderSecrets secrets;
        }

        public static ShaderSecrets GetSecrets()
        {
            return LoadOrCreateSettings().secrets;
        }

        // Brings the copy up to date and returns the shader of the given container, e.g. "lts" for lts.lilcontainer.
        public static Shader GetShader(string runtimeDir, string containerName)
        {
            Settings settings = LoadOrCreateSettings();
            string sourceDir = OutputPaths.Combine(runtimeDir, "liltoonProtector", "Shaders");
            string protectorPath = OutputPaths.Combine(runtimeDir, "Shader", "Protector.cginc");

            bool changed = false;
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                string text = Transform(fileName, File.ReadAllText(file), settings, protectorPath);
                if (text == null)
                    return null;
                changed |= WriteIfChanged(OutputPaths.Combine(Folder, fileName), text);
            }
            if (changed)
                AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<Shader>(OutputPaths.Combine(Folder, containerName + ".lilcontainer"));
        }

        static string Transform(string fileName, string text, Settings settings, string protectorPath)
        {
            if (fileName == DataFile)
            {
                var shaderName = new Regex("ShaderName \"(.*?)\"");
                if (!shaderName.IsMatch(text))
                {
                    Debug.LogErrorFormat("[ShellProtector] No ShaderName in {0}", fileName);
                    return null;
                }
                return shaderName.Replace(text, m => "ShaderName \"" + m.Groups[1].Value + "_" + settings.id + "\"", 1);
            }
            if (fileName == InsertFile)
            {
                // The package includes Protector.cginc by a relative path that doesn't resolve from the copy.
                var include = new Regex("#include \"[^\"]*Protector\\.cginc\"");
                if (!include.IsMatch(text))
                {
                    Debug.LogErrorFormat("[ShellProtector] No Protector.cginc include in {0}", fileName);
                    return null;
                }
                return include.Replace(text, m => settings.secrets.ToDefines() + "#include \"" + protectorPath + "\"", 1);
            }
            return text;
        }

        static bool WriteIfChanged(string path, string text)
        {
            if (File.Exists(path) && File.ReadAllText(path) == text)
                return false;
            File.WriteAllText(path, text);
            return true;
        }

        static Settings LoadOrCreateSettings()
        {
            string path = OutputPaths.Combine(Folder, SettingsFile);
            if (File.Exists(path))
            {
                try
                {
                    Settings loaded = JsonUtility.FromJson<Settings>(File.ReadAllText(path));
                    if (loaded != null && !string.IsNullOrEmpty(loaded.id) && loaded.secrets != null && loaded.secrets.IsValid)
                        return loaded;
                }
                catch (ArgumentException e)
                {
                    Debug.LogWarningFormat("[ShellProtector] {0} is invalid and is replaced: {1}", path, e.Message);
                }
            }

            // New secrets change the copy, so lilToon materials encrypted before decrypt to noise until they are encrypted again.
            var settings = new Settings { id = RandomId(), secrets = ShaderSecrets.Generate() };
            Directory.CreateDirectory(Folder);
            File.WriteAllText(path, JsonUtility.ToJson(settings, true));
            return settings;
        }

        static string RandomId()
        {
            byte[] bytes = new byte[4];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
#endif
