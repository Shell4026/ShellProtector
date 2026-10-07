#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        // Hash of the package's .cginc files that the copy includes by path (see GetShader).
        const string IncludesFile = "ShellProtectorIncludes.txt";

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
            string shaderDir = OutputPaths.Combine(runtimeDir, "Shader");

            var changed = new HashSet<string>();
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                string text = Transform(fileName, File.ReadAllText(file), settings, shaderDir);
                if (text == null)
                    return null;
                if (WriteIfChanged(OutputPaths.Combine(Folder, fileName), text))
                    changed.Add(fileName);
            }
            // The containers are generated from the blocks and include the package's .cginc files by path, and the lilToon importer
            // doesn't track either. So when anything but a container changes, the containers that weren't rewritten are imported again.
            if (WriteIfChanged(OutputPaths.Combine(Folder, IncludesFile), HashIncludes(shaderDir)))
                changed.Add(IncludesFile);
            if (changed.Count > 0)
            {
                AssetDatabase.Refresh();
                if (changed.Any(f => !f.EndsWith(".lilcontainer", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (string container in Directory.GetFiles(Folder, "*.lilcontainer"))
                    {
                        if (!changed.Contains(Path.GetFileName(container)))
                            AssetDatabase.ImportAsset(OutputPaths.Normalize(container), ImportAssetOptions.ForceUpdate);
                    }
                }
            }

            return AssetDatabase.LoadAssetAtPath<Shader>(OutputPaths.Combine(Folder, containerName + ".lilcontainer"));
        }

        static string Transform(string fileName, string text, Settings settings, string shaderDir)
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
                // The package includes Protector.cginc and Emission.cginc by relative paths that don't resolve from the copy.
                var include = new Regex("#include \"[^\"]*/Shader/(\\w+\\.cginc)\"");
                if (!Regex.IsMatch(text, "#include \"[^\"]*/Shader/Protector\\.cginc\""))
                {
                    Debug.LogErrorFormat("[ShellProtector] No Protector.cginc include in {0}", fileName);
                    return null;
                }
                return include.Replace(text, m =>
                {
                    string path = "#include \"" + OutputPaths.Combine(shaderDir, m.Groups[1].Value) + "\"";
                    return m.Groups[1].Value == "Protector.cginc" ? settings.secrets.ToDefines() + path : path;
                });
            }
            return text;
        }

        static string HashIncludes(string shaderDir)
        {
            using (SHA256 sha = SHA256.Create())
            {
                foreach (string file in Directory.GetFiles(shaderDir, "*.cginc").OrderBy(f => f, StringComparer.Ordinal))
                {
                    byte[] name = Encoding.UTF8.GetBytes(Path.GetFileName(file));
                    byte[] data = File.ReadAllBytes(file);
                    sha.TransformBlock(name, 0, name.Length, null, 0);
                    sha.TransformBlock(data, 0, data.Length, null, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
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
