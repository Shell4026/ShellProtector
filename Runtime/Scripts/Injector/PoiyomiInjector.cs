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
        protected override Shader CustomInject(Material mat, string decode_dir, string output_path, Texture2D tex, bool has_lim_texture = false, bool has_lim_texture2 = false, bool outline_tex = false)
        {
            if (!File.Exists(decode_dir))
            {
                Debug.LogError(decode_dir + " is not exits.");
                return null;
            }

            Shader shader = mat.shader;

            if (!AssetDatabase.IsValidFolder(output_path))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(output_path), Path.GetFileName(output_path));
                AssetDatabase.Refresh();
            }

            string shader_path = AssetDatabase.GetAssetPath(shader);
            string shader_name = Path.GetFileName(shader_path);

            string[] files = Directory.GetFiles(Path.GetDirectoryName(shader_path));
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Contains(".meta"))
                    continue;
                File.Copy(file, Path.Combine(output_path, filename), true);
            }

            string shader_data = File.ReadAllText(Path.Combine(output_path, shader_name));
            shader_data = shader_data.Insert(0, "//ShellProtect\n");

            InsertProperties(ref shader_data);
            if (shader_data == null)
                return null;

            const string declare = @"
            UNITY_DECLARE_TEX2D(_MainTex);
            UNITY_DECLARE_TEX2D(_MipTex);
            UNITY_DECLARE_TEX2D(_EncryptTex0);
            Texture2D _EncryptTex1;

            float4 _EncryptTex0_TexelSize;
            int _PasswordHash;
";
            int version = AssetManager.GetInstance().GetShaderType(shader);
            if(version == 73)
            {
                string path = output_path + "/CGI_Poicludes.cginc";
                string poicludes = File.ReadAllText(path);
                poicludes = Regex.Replace(poicludes, "UNITY_DECLARE_TEX2D\\(_MainTex\\);(.*?)", declare);
                File.WriteAllText(path, poicludes);

                path = output_path + "/CGI_PoiFrag.cginc";
                string frag = File.ReadAllText(path);
                frag = Regex.Replace(frag, "float4 frag\\(", "#include \"Decrypt.cginc\"\nfloat4 frag(");

                string shader_code = shader_code_nofilter;
                if (filter == 0)
                    shader_code = shader_code_nofilter;
                else if (filter == 1)
                    shader_code = shader_code_bilinear;

                frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code);
                frag = Regex.Replace(frag, "float4 mip_texture = _MipTex.Sample\\(sampler_MipTex, .*?\\);", "float4 mip_texture = _MipTex.Sample(sampler_MipTex, poiMesh.uv[0]);");
                File.WriteAllText(path, frag);
            }
            else if(version >= 80)
            {
                shader_data = Regex.Replace(shader_data, "UNITY_DECLARE_TEX2D\\(_MainTex\\);", declare);

                shader_data = Regex.Replace(shader_data, "POI2D_SAMPLER_PAN\\((.*?), _MainTex", "POI2D_SAMPLER_PAN($1, _MipTex");
                shader_data = Regex.Replace(shader_data, "UNITY_SAMPLE_TEX2D_SAMPLER_LOD\\((.*?), _MainTex", "UNITY_SAMPLE_TEX2D_SAMPLER_LOD($1, _MipTex");
                shader_data = Regex.Replace(shader_data, "UNITY_SAMPLE_TEX2D_SAMPLER\\((.*?), _MainTex", "UNITY_SAMPLE_TEX2D_SAMPLER($1, _MipTex");
                shader_data = Regex.Replace(shader_data, "float4 frag\\(", "#include \"" + decode_dir + "\"\n\t\t\tfloat4 frag(");
                string shader_code = shader_code_nofilter;
                if (filter == 0)
                    shader_code = shader_code_nofilter;
                else if (filter == 1)
                    shader_code = shader_code_bilinear;

                shader_data = Regex.Replace(shader_data, "float4 mainTexture = .*?;", shader_code);
                if (has_lim_texture)
                {
                    if(version == 80)
                        shader_data = Regex.Replace(shader_data, @"float4 rimColor = .*?_RimTex.*?;", "float4 rimColor = float4(poiFragData.baseColor, poiFragData.alpha);");
                    else
                        shader_data = Regex.Replace(shader_data, @"float4 rimColor = .*?_RimTex.*?;", "float4 rimColor = mainTexture;");
                }
                if (has_lim_texture2)
                {
                    if(version == 80)
                        shader_data = Regex.Replace(shader_data, @"float4 rim2Color = .*?_Rim2Tex.*?;", "float4 rim2Color = float4(poiFragData.baseColor, poiFragData.alpha);");
                    else
                        shader_data = Regex.Replace(shader_data, @"float4 rim2Color = .*?_Rim2Tex.*?;", "float4 rim2Color = mainTexture;");
                }
                if (outline_tex)
                    shader_data = Regex.Replace(shader_data, @"float4 col = .*?_OutlineTexture.*?\* float4(.*?);", "float4 col = float4(poiFragData.baseColor, poiFragData.alpha) * float4$1;");

            }
            else
            {
                Debug.LogErrorFormat("{0} is unsupported Poiyomi version!", mat.name);
                return null;
            }
            File.WriteAllText(Path.Combine(output_path, shader_name), shader_data);

            AssetDatabase.Refresh();

            Shader return_shader = AssetDatabase.LoadAssetAtPath(Path.Combine(output_path, shader_name), typeof(Shader)) as Shader;
            return return_shader;
        }

        private void InsertProperties(ref string data)
        {
            data = Regex.Replace(data, "Shader \"(.*?)\"", "Shader \"$1_encrypted\""); //shader name change

            //properties insert
            Match match = Regex.Match(data, "Properties\\W*{");
            if (match.Success)
            {
                int suffix_idx = match.Index + match.Length;
                string properties = @"
        _MipTex (""MipReference"", 2D) = ""white"" { }
        _EncryptTex0 (""Encrypted0"", 2D) = ""white"" { }
        _EncryptTex1 (""Encrypted1"", 2D) = ""white"" { }
        _Woffset (""Woffset"", integer) = 0
        _Hoffset (""Hoffset"", integer) = 0
        _Nonce0 (""Nonce"", integer) = 0
        _Nonce1 (""Nonce"", integer) = 0
        _Nonce2 (""Nonce"", integer) = 0
        _PasswordHash (""PasswordHash"", integer) = 0
        _HashMagic (""HashMagic"", integer) = 0
";

                for (int i = 0; i < 16; ++i)
                    properties += "_Key" + i + " (\"key" + i + "\", float) = 0\n";

                data = data.Insert(suffix_idx, properties);
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