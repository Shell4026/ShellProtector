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
        public override Shader Inject(Material mat, string decode_dir, Texture2D tex, bool has_lim_texture = false, bool has_lim_texture2 = false, bool outline_tex = false)
        {
            if (!File.Exists(decode_dir))
            {
                Debug.LogError(decode_dir + " is not exits.");
                return null;
            }

            Shader shader = mat.shader;

            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, target.name, "shader", mat.name)))
            {
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, target.name, "shader"), mat.name);
                AssetDatabase.Refresh();
            }

            string shader_path = AssetDatabase.GetAssetPath(shader);
            string shader_name = Path.GetFileName(shader_path);
            string output_path = Path.Combine(asset_dir, target.name, "shader", mat.name);

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

            string declare = @"
            UNITY_DECLARE_TEX2D(_MainTex);
            UNITY_DECLARE_TEX2D(_MipTex);
            Texture2D _EncryptTex;
";
            int version = ShaderManager.GetInstance().GetShaderType(shader);
            if(version == 73)
            {
                string path = output_path + "/CGI_Poicludes.cginc";
                string poicludes = File.ReadAllText(path);
                poicludes = Regex.Replace(poicludes, "UNITY_DECLARE_TEX2D\\(_MainTex\\);(.*?)", declare);
                File.WriteAllText(path, poicludes);

                path = output_path + "/CGI_PoiFrag.cginc";
                string frag = File.ReadAllText(path);
                frag = Regex.Replace(frag, "float4 frag\\(", "#include \"Decrypt.cginc\"\nfloat4 frag(");

                string shader_code = shader_code_nofilter_XXTEA;
                if (filter == 0)
                    shader_code = shader_code_nofilter_XXTEA;
                else if (filter == 1)
                    shader_code = shader_code_bilinear_XXTEA;

                frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code);
                frag = Regex.Replace(frag, "float4 mip_texture = _MipTex.Sample\\(sampler_MipTex, .*?\\);", "float4 mip_texture = _MipTex.Sample(sampler_MipTex, poiMesh.uv[0]);");
                if (tex.format == TextureFormat.DXT1)
                {
                    frag = Regex.Replace(frag, "DecryptTextureXXTEA", "DecryptTextureXXTEADXT");
                }
                else if (EncryptTexture.HasAlpha(tex))
                {
                    frag = Regex.Replace(frag, "DecryptTextureXXTEA", "DecryptTextureXXTEARGBA");
                }
                File.WriteAllText(path, frag);
            }
            else if(version >= 80)
            {
                shader_data = Regex.Replace(shader_data, "UNITY_DECLARE_TEX2D\\(_MainTex\\);", declare);

                shader_data = Regex.Replace(shader_data, "POI2D_SAMPLER_PAN\\((.*?), _MainTex", "POI2D_SAMPLER_PAN($1, _MipTex");
                shader_data = Regex.Replace(shader_data, "UNITY_SAMPLE_TEX2D_SAMPLER_LOD\\((.*?), _MainTex", "UNITY_SAMPLE_TEX2D_SAMPLER_LOD($1, _MipTex");
                shader_data = Regex.Replace(shader_data, "UNITY_SAMPLE_TEX2D_SAMPLER\\((.*?), _MainTex", "UNITY_SAMPLE_TEX2D_SAMPLER($1, _MipTex");
                shader_data = Regex.Replace(shader_data, "float4 frag\\(", "#include \"Decrypt.cginc\"\n\t\t\tfloat4 frag(");
                string shader_code = shader_code_nofilter_XXTEA;
                if (filter == 0)
                    shader_code = shader_code_nofilter_XXTEA;
                else if (filter == 1)
                    shader_code = shader_code_bilinear_XXTEA;

                shader_data = Regex.Replace(shader_data, "float4 mainTexture = .*?;", shader_code);
                if (tex.format == TextureFormat.DXT1 || tex.format == TextureFormat.DXT5)
                {
                    shader_data = Regex.Replace(shader_data, "DecryptTextureXXTEA", "DecryptTextureXXTEADXT");
                }
                else if (EncryptTexture.HasAlpha(tex))
                {
                    shader_data = Regex.Replace(shader_data, "DecryptTextureXXTEA", "DecryptTextureXXTEARGBA");
                }

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
            File.WriteAllText(output_path + '/' + shader_name, shader_data);

            string decode_data = GenerateDecoder(decode_dir, tex);
            if (decode_data == null)
                return null;

            File.WriteAllText(output_path + "/Decrypt.cginc", decode_data);
            AssetDatabase.Refresh();

            Shader return_shader = AssetDatabase.LoadAssetAtPath(output_path + '/' + shader_name, typeof(Shader)) as Shader;
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
        _EncryptTex (""Encrypted"", 2D) = ""white"" { }";

                for (int i = 0; i < user_key_length; ++i)
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