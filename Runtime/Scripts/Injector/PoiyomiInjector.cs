#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    public class PoiyomiInjector : Injector
    {
        public override bool CanHandle(Shader shader)
        {
            return ShaderManager.IsPoiyomi(shader);
        }

        protected override Shader CustomInject(Material mat, string decodeDir, string outputPath, Texture2D tex, bool hasLimTexture = false, bool hasLimTexture2 = false, bool outlineTex = false)
        {
            if (!File.Exists(decodeDir))
            {
                Debug.LogError(decodeDir + " is not exits.");
                return null;
            }

            Shader shader = mat.shader;

            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(outputPath), Path.GetFileName(outputPath));
                AssetDatabase.Refresh();
            }

            string shaderPath = AssetDatabase.GetAssetPath(shader);
            string shaderName = Path.GetFileName(shaderPath);

            string[] files = Directory.GetFiles(Path.GetDirectoryName(shaderPath));
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Contains(".meta"))
                    continue;
                File.Copy(file, Path.Combine(outputPath, filename), true);
            }

            string shaderData = File.ReadAllText(Path.Combine(outputPath, shaderName));
            shaderData = BuildShaderSource(shader, shaderData, decodeDir, hasLimTexture, hasLimTexture2, outlineTex);
            if (shaderData == null) return null;
            File.WriteAllText(Path.Combine(outputPath, shaderName), shaderData);
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<Shader>(Path.Combine(outputPath, shaderName));
        }

        // Shared by asset generation and in-memory editor previews.
        public string BuildShaderSource(Shader shader, string shaderData, string decodeDir, bool hasLimTexture = false, bool hasLimTexture2 = false, bool outlineTex = false)
        {
            shaderData = shaderData.Insert(0, "//ShellProtect\n");

            InsertProperties(ref shaderData);
            if (shaderData == null)
                return null;

            const string declare = @"
            UNITY_DECLARE_TEX2D(_MainTex);
            UNITY_DECLARE_TEX2D(_MipTex);
            UNITY_DECLARE_TEX2D(_EncryptTex0);
            Texture2D _EncryptTex1;

            float4 _EncryptTex0_TexelSize;
            int _PasswordHash;
";
            const string vertexOut = @"
			struct VertexOut
			{
				int isDecrypted : SHELL0;";

            int version = AssetManager.GetInstance().GetShaderType(shader);
            if(version >= 80)
            {
                string includeStr = string.Format("#include \"{0}\"", decodeDir);
                shaderData = Regex.Replace(shaderData, "UNITY_DECLARE_TEX2D\\(_MainTex\\);", declare);

                shaderData = Regex.Replace(shaderData, "POI2D_SAMPLER_PAN\\((.*?), _MainTex", "POI2D_SAMPLER_PAN($1, _MipTex");
                shaderData = Regex.Replace(shaderData, "UNITY_SAMPLE_TEX2D_SAMPLER_LOD\\((.*?), _MainTex", "UNITY_SAMPLE_TEX2D_SAMPLER_LOD($1, _MipTex");
                shaderData = Regex.Replace(shaderData, "UNITY_SAMPLE_TEX2D_SAMPLER\\((.*?), _MainTex", "UNITY_SAMPLE_TEX2D_SAMPLER($1, _MipTex");
                shaderData = Regex.Replace(shaderData, "float4 frag\\(", "#include \"" + decodeDir + "\"\n\t\t\tfloat4 frag(");
                shaderData = Regex.Replace(shaderData, "struct VertexOut[\r\n]+[ \t]*\\{", string.Format("{0}\r\n{1}", includeStr, vertexOut));
                shaderData = InjectVertexDecryptionState(shaderData);
                string shaderCode = ShaderCodeNoFilter;
                if (Filter == 0)
                    shaderCode = ShaderCodeNoFilter;
                else if (Filter == 1)
                    shaderCode = ShaderCodeBilinear;

                shaderData = Regex.Replace(shaderData, "float4 mainTexture = .*?;", string.Format("bool isDecrypted = i.isDecrypted == 1;\r\n{0}", shaderCode));
                if (hasLimTexture)
                {
                    if(version == 80)
                        shaderData = Regex.Replace(shaderData, @"float4 rimColor = .*?_RimTex.*?;", "float4 rimColor = float4(poiFragData.baseColor, poiFragData.alpha);");
                    else
                        shaderData = Regex.Replace(shaderData, @"float4 rimColor = .*?_RimTex.*?;", "float4 rimColor = mainTexture;");
                }
                if (hasLimTexture2)
                {
                    if(version == 80)
                        shaderData = Regex.Replace(shaderData, @"float4 rim2Color = .*?_Rim2Tex.*?;", "float4 rim2Color = float4(poiFragData.baseColor, poiFragData.alpha);");
                    else
                        shaderData = Regex.Replace(shaderData, @"float4 rim2Color = .*?_Rim2Tex.*?;", "float4 rim2Color = mainTexture;");
                }
                if (outlineTex)
                    shaderData = Regex.Replace(shaderData, @"float4 col = .*?_OutlineTexture.*?\* float4(.*?);", "float4 col = float4(poiFragData.baseColor, poiFragData.alpha) * float4$1;");

            }
            else
            {
                Debug.LogErrorFormat("{0} is unsupported Poiyomi version!", shader.name);
                return null;
            }
            return shaderData;
        }

        private static string InjectVertexDecryptionState(string shaderData)
        {
            // Match a complete vertex function, including nested blocks. Other functions
            // (notably Wrapped lighting's RTWrapFunc) also return a variable named o.
            const string vertexFunction = @"\bVertexOut\s+vert\s*\([^)]*\)\s*\{
                (?: //[^\r\n]* | /\*[\s\S]*?\*/ | [^{}\/]+ | /(?![/*])
                  | \{ (?<depth>) | \} (?<-depth>) )*
                (?(depth)(?!)) \}";

            return Regex.Replace(shaderData, vertexFunction, match =>
                Regex.Replace(match.Value, @"\breturn\s+o\s*;",
                    "o.isDecrypted = IsDecrypted();\n                return o;"),
                RegexOptions.IgnorePatternWhitespace);
        }

        private void InsertProperties(ref string data)
        {
            data = Regex.Replace(data, "Shader \"(.*?)\"", "Shader \"$1_encrypted\""); //shader name change

            //properties insert
            Match match = Regex.Match(data, "Properties\\W*{");
            if (match.Success)
            {
                int suffixIndex = match.Index + match.Length;
                string properties = @"
" + ShaderProperties.MipTexture + @" (""MipReference"", 2D) = ""white"" { }
" + ShaderProperties.EncryptTexture0 + @" (""Encrypted0"", 2D) = ""white"" { }
" + ShaderProperties.EncryptTexture1 + @" (""Encrypted1"", 2D) = ""white"" { }
" + ShaderProperties.WidthOffset + @" (""Woffset"", integer) = 0
" + ShaderProperties.HeightOffset + @" (""Hoffset"", integer) = 0
" + ShaderProperties.Nonce0 + @" (""Nonce"", integer) = 0
" + ShaderProperties.Nonce1 + @" (""Nonce"", integer) = 0
" + ShaderProperties.Nonce2 + @" (""Nonce"", integer) = 0
" + ShaderProperties.Rounds + @" (""Rounds"", integer) = 0
" + ShaderProperties.PasswordHash + @" (""PasswordHash"", integer) = 0
" + ShaderProperties.HashMagic + @" (""HashMagic"", integer) = 0
[HideInInspector] " + ShaderProperties.BC7LayoutVersion + @" (""BC7 layout"", Integer) = 0
[HideInInspector] " + ShaderProperties.SourceTexelSize + @" (""Source size"", Vector) = (1,1,1,1)
[HideInInspector] " + ShaderProperties.SourceSampling + @" (""Source sampling"", Vector) = (1,1,0,0)
";

                for (int i = 0; i < 4; ++i)
                    properties += "[HideInInspector] " + ShaderProperties.MipOffsetsPrefix + i + " (\"Mip offsets\", Vector) = (0,0,0,0)\n";
                // Sampling is baked into this injected shader. Cache lookup must distinguish it.
                properties += "[HideInInspector] " + ShaderProperties.InjectedFilter + " (\"Injected filter\", Integer) = " + Filter + "\n";

                for (int i = 0; i < 16; ++i)
                    properties += ShaderProperties.KeyPrefix + i + " (\"key" + i + "\", float) = 0\n";

                data = data.Insert(suffixIndex, properties);
            }
            else
            {
                Debug.LogError("Wrong shader data!");
                data = null;
            }
        }
    }
}
#endif
