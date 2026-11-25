#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shell.Protector
{
    // Locking a Poiyomi material bakes its property values into the locked shader's code, so the material's copies
    // are never read but would still show every setting in a ripped material. Which names the code still reads is
    // found by scanning the shader source, so it works for every Poiyomi version without knowing its properties.
    public sealed class MaterialObfuscator
    {
        // VRChat's fallback shaders and Unity's built-in includes read these. Thry's upload check reads its lock state.
        static readonly HashSet<string> AlwaysKept = new HashSet<string>
        {
            "_MainTex", "_Color", "_Cutoff", "_EmissionMap", "_EmissionColor", "_BumpMap", "_BumpScale",
            "_MetallicGlossMap", "_Metallic", "_Glossiness", "_GlossMapScale", "_OcclusionMap", "_OcclusionStrength",
            "_SpecColor", "_ShaderOptimizerEnabled"
        };
        // OriginalShader stays: Thry and ShellProtector tell a locked material by it.
        static readonly string[] RemovedTags = { "OriginalKeywords", "AllLockedGUIDS" };
        const string TexturePackTag = "_texPack_";

        static readonly Regex Identifier = new Regex(@"\b[A-Za-z_]\w*\b", RegexOptions.Compiled);
        static readonly Regex Include = new Regex(@"^\s*#\s*include\s+""([^""]+)""", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex MacroDefinition = new Regex(@"^\s*#\s*define\s+(\w+)\s*\(([^)]*)\)(.*)$", RegexOptions.Compiled);
        static readonly Regex TokenPaste = new Regex(@"(\w+)\s*##\s*(\w+)", RegexOptions.Compiled);
        // A line that only declares names: "half _X;", "Texture2D _X;", "UNITY_DECLARE_TEX2D(_X);", "POI_DECLARETEX_ST_UV(_X)".
        static readonly Regex Declaration = new Regex(
            @"^\s*(?:uniform\s+)?(?:float|half|fixed|int|uint|bool|double|min16float|min10float)(?:[1-4](?:x[1-4])?)?\s+\w+\s*;\s*$" +
            @"|^\s*(?:Texture2D|Texture2DArray|Texture3D|TextureCube|TextureCubeArray|sampler2D|sampler3D|samplerCUBE|SamplerState)(?:\s*<[^>]*>)?\s+\w+\s*;\s*$" +
            @"|^\s*\w*(?:DECLARE|DEFINE_INSTANCED_PROP)\w*\s*\([^;]*\)\s*;?\s*$",
            RegexOptions.Compiled);

        readonly Dictionary<Shader, HashSet<string>> readIdentifiers = new Dictionary<Shader, HashSet<string>>();

        // Returns -1 when the shader source can't be read and the material is left as it is.
        public int Apply(Material material, Material source)
        {
            if (!readIdentifiers.TryGetValue(material.shader, out HashSet<string> read))
            {
                read = ReadShaderIdentifiers(material.shader, source.shader);
                readIdentifiers.Add(material.shader, read);
            }
            if (read == null)
            {
                Debug.LogWarningFormat("[ShellProtector] {0}: can't read the shader source, so the material isn't obfuscated.", source.name);
                return -1;
            }
            return Apply(material, source, read);
        }

        // source's shader tells the encrypted copy's own properties apart, and its tags the animated ones.
        public static int Apply(Material material, Material source, HashSet<string> read)
        {
            Shader shader = material.shader;
            var original = new HashSet<string>();
            for (int i = 0; i < source.shader.GetPropertyCount(); i++)
                original.Add(source.shader.GetPropertyName(i));

            int reset = 0;
            var types = new Dictionary<string, ShaderPropertyType>();
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string name = shader.GetPropertyName(i);
                ShaderPropertyType type = shader.GetPropertyType(i);
                types[name] = type;
                if ((shader.GetPropertyFlags(i) & ShaderPropertyFlags.NonModifiableTextureData) != 0)
                    continue;

                bool kept = AlwaysKept.Contains(name) || !original.Contains(name) || IsAnimated(source, name);
                if (type == ShaderPropertyType.Texture)
                {
                    bool sampled = read.Contains(name) || read.Contains("sampler" + name) || read.Contains(name + "_TexelSize") || read.Contains(name + "_HDR");
                    if (!kept && !sampled && material.GetTexture(name) != null)
                    {
                        material.SetTexture(name, null);
                        reset++;
                    }
                    if (!kept && !read.Contains(name + "_ST") && (material.GetTextureScale(name) != Vector2.one || material.GetTextureOffset(name) != Vector2.zero))
                    {
                        material.SetTextureScale(name, Vector2.one);
                        material.SetTextureOffset(name, Vector2.zero);
                        reset++;
                    }
                    continue;
                }
                if (kept || read.Contains(name))
                    continue;

                switch (type)
                {
                    case ShaderPropertyType.Color:
                    case ShaderPropertyType.Vector:
                        material.SetVector(name, shader.GetPropertyDefaultVectorValue(i));
                        break;
                    case ShaderPropertyType.Int:
                        material.SetInteger(name, shader.GetPropertyDefaultIntValue(i));
                        break;
                    default:
                        material.SetFloat(name, shader.GetPropertyDefaultFloatValue(i));
                        break;
                }
                reset++;
            }

            reset += RemoveStaleData(material, types);
            return reset;
        }

        static bool IsAnimated(Material source, string name)
        {
            string tag = source.GetTag(name + "Animated", false, "");
            return tag != "" && tag != "0";
        }

        static int RemoveStaleData(Material material, Dictionary<string, ShaderPropertyType> types)
        {
            var serialized = new SerializedObject(material);
            SerializedProperty saved = serialized.FindProperty("m_SavedProperties");
            int removed = 0;
            removed += RemoveEntries(saved.FindPropertyRelative("m_TexEnvs"), name => !HasType(types, name, ShaderPropertyType.Texture));
            removed += RemoveEntries(saved.FindPropertyRelative("m_Floats"), name => !HasType(types, name, ShaderPropertyType.Float, ShaderPropertyType.Range));
            removed += RemoveEntries(saved.FindPropertyRelative("m_Ints"), name => !HasType(types, name, ShaderPropertyType.Int));
            removed += RemoveEntries(saved.FindPropertyRelative("m_Colors"), name => !HasType(types, name, ShaderPropertyType.Color, ShaderPropertyType.Vector));
            RemoveEntries(serialized.FindProperty("stringTagMap"), name => Array.IndexOf(RemovedTags, name) >= 0 || name.Contains(TexturePackTag));
            SerializedProperty invalidKeywords = serialized.FindProperty("m_InvalidKeywords");
            if (invalidKeywords != null && invalidKeywords.isArray)
                invalidKeywords.ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return removed;
        }

        static bool HasType(Dictionary<string, ShaderPropertyType> types, string name, params ShaderPropertyType[] allowed)
        {
            return types.TryGetValue(name, out ShaderPropertyType type) && Array.IndexOf(allowed, type) >= 0;
        }

        static int RemoveEntries(SerializedProperty map, Func<string, bool> remove)
        {
            if (map == null || !map.isArray)
                return 0;
            int removed = 0;
            for (int i = map.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty key = map.GetArrayElementAtIndex(i).FindPropertyRelative("first");
                if (key != null && remove(key.stringValue))
                {
                    map.DeleteArrayElementAtIndex(i);
                    removed++;
                }
            }
            return removed;
        }

        // The encrypted copy lives in the output folder, and a locked shader's own includes next to the locked shader,
        // so includes are looked up next to both.
        static HashSet<string> ReadShaderIdentifiers(Shader shader, Shader originalShader)
        {
            string path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            var folders = new List<string> { Path.GetDirectoryName(path) };
            string originalPath = AssetDatabase.GetAssetPath(originalShader);
            if (!string.IsNullOrEmpty(originalPath))
                folders.Add(Path.GetDirectoryName(originalPath));

            return FindReadIdentifiers(File.ReadAllText(path), include =>
            {
                string file = ResolveInclude(include, folders);
                if (file == null)
                    return null;
                string folder = Path.GetDirectoryName(file);
                if (!folders.Contains(folder))
                    folders.Add(folder);
                return File.ReadAllText(file);
            });
        }

        // An include that isn't found is one of Unity's own (UnityCG.cginc), which reads none of the material's properties.
        static string ResolveInclude(string include, List<string> folders)
        {
            string normalized = include.Replace('\\', '/');
            if ((normalized.StartsWith("Assets/") || normalized.StartsWith("Packages/")) && File.Exists(normalized))
                return normalized;
            foreach (string folder in folders)
            {
                string candidate = Path.Combine(folder, normalized.TrimStart('/'));
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        // The Properties block, comments and lines that only declare a uniform don't count. A macro that pastes a suffix
        // onto an argument (tex##_ST) adds that suffix to every name read.
        public static HashSet<string> FindReadIdentifiers(string shaderSource, Func<string, string> loadInclude)
        {
            var read = new HashSet<string>();
            var suffixes = new HashSet<string>();
            var prefixes = new HashSet<string>();
            var visited = new HashSet<string>();
            Scan(RemovePropertiesBlock(shaderSource), loadInclude, read, suffixes, prefixes, visited);

            var pasted = new List<string>();
            foreach (string name in read)
            {
                foreach (string suffix in suffixes)
                    pasted.Add(name + suffix);
                foreach (string prefix in prefixes)
                    pasted.Add(prefix + name);
            }
            read.UnionWith(pasted);
            return read;
        }

        static void Scan(string source, Func<string, string> loadInclude, HashSet<string> read, HashSet<string> suffixes, HashSet<string> prefixes, HashSet<string> visited)
        {
            string code = RemoveComments(source).Replace("\\\r\n", " ").Replace("\\\n", " ");
            foreach (string line in code.Split('\n'))
            {
                Match macro = MacroDefinition.Match(line);
                if (macro.Success && !macro.Groups[1].Value.Contains("DECLARE"))
                {
                    var parameters = new HashSet<string>();
                    foreach (string parameter in macro.Groups[2].Value.Split(','))
                        parameters.Add(parameter.Trim());
                    foreach (Match paste in TokenPaste.Matches(macro.Groups[3].Value))
                    {
                        if (parameters.Contains(paste.Groups[1].Value))
                            suffixes.Add(paste.Groups[2].Value);
                        else if (parameters.Contains(paste.Groups[2].Value))
                            prefixes.Add(paste.Groups[1].Value);
                    }
                }
                if (Declaration.IsMatch(line))
                    continue;
                foreach (Match identifier in Identifier.Matches(line))
                    read.Add(identifier.Value);
            }

            foreach (Match include in Include.Matches(code))
            {
                string name = include.Groups[1].Value;
                if (!visited.Add(name))
                    continue;
                string text = loadInclude?.Invoke(name);
                if (text != null)
                    Scan(text, loadInclude, read, suffixes, prefixes, visited);
            }
        }

        // Display names hold braces and other property names ("--{condition_showS:(_LightingMode==1)}"), so braces
        // inside strings and comments don't count. If the end isn't found the source is returned whole, which only
        // keeps more values.
        public static string RemovePropertiesBlock(string shaderSource)
        {
            Match properties = Regex.Match(shaderSource, @"^[ \t]*Properties\s*(?=\{|$)", RegexOptions.Multiline);
            if (!properties.Success)
                return shaderSource;
            int open = shaderSource.IndexOf('{', properties.Index + properties.Length);
            if (open < 0)
                return shaderSource;

            int depth = 0;
            bool inString = false;
            for (int i = open; i < shaderSource.Length; i++)
            {
                char c = shaderSource[i];
                char next = i + 1 < shaderSource.Length ? shaderSource[i + 1] : '\0';
                if (inString)
                {
                    if (c == '\\')
                        i++;
                    else if (c == '"')
                        inString = false;
                }
                else if (c == '/' && next == '/')
                {
                    int end = shaderSource.IndexOf('\n', i);
                    i = end < 0 ? shaderSource.Length : end;
                }
                else if (c == '/' && next == '*')
                {
                    int end = shaderSource.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0)
                        return shaderSource;
                    i = end + 1;
                }
                else if (c == '"')
                    inString = true;
                else if (c == '{')
                    depth++;
                else if (c == '}' && --depth == 0)
                    return shaderSource.Substring(0, properties.Index) + shaderSource.Substring(i + 1);
            }
            return shaderSource;
        }

        static string RemoveComments(string source)
        {
            var result = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n')
                        i++;
                    if (i < source.Length)
                        result.Append('\n');
                }
                else if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    int stop = end < 0 ? source.Length : end + 2;
                    for (int j = i; j < stop; j++)
                    {
                        if (source[j] == '\n')
                            result.Append('\n');
                    }
                    i = stop - 1;
                }
                else
                    result.Append(source[i]);
            }
            return result.ToString();
        }
    }
}
#endif
