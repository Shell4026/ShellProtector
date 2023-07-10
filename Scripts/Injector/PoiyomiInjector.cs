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
        public override Shader Inject(Material mat, string decode_dir, Texture2D tex)
        {
            if (!File.Exists(decode_dir))
            {
                Debug.LogError(decode_dir + " is not exits.");
                return null;
            }

            Shader shader = mat.shader;

            if (!AssetDatabase.IsValidFolder(asset_dir + '/' + target.name + "/shader/" + mat.name))
            {
                AssetDatabase.CreateFolder(asset_dir + '/' + target.name + "/shader", mat.name);
                AssetDatabase.Refresh();
            }

            string shader_path = AssetDatabase.GetAssetPath(shader);
            string shader_name = Path.GetFileName(shader_path);
            string output_path = asset_dir + '/' + target.name + "/shader/" + mat.name;

            string[] files = Directory.GetFiles(Path.GetDirectoryName(shader_path));
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Contains(".meta"))
                    continue;
                File.Copy(file, Path.Combine(output_path, filename), true);
            }

            string shader_data = File.ReadAllText(output_path + "/" + shader_name);
            shader_data = shader_data.Insert(0, "//ShellProtect\n");

            shader_data = Regex.Replace(shader_data, "Shader \"(.*?)\"", "Shader \"$1_encrypted\""); //shader name change

            //properties insert
            Match match = Regex.Match(shader_data, "Properties\\W*{");
            if (match.Success)
            {
                int suffix_idx = match.Index + match.Length;
                string properties = @"
        _MipTex (""MipReference"", 2D) = ""white"" { }
        _EncryptTex (""Encrypted"", 2D) = ""white"" { }
        _Key0 (""key0"", int) = 0
        _Key1 (""key1"", int) = 0
        _Key2 (""key2"", int) = 0
        _Key3 (""key3"", int) = 0";
                shader_data = shader_data.Insert(suffix_idx, properties);
            }
            else
            {
                Debug.LogError("Wrong shader data!");
                return null;
            }

            string declare = @"
            UNITY_DECLARE_TEX2D(_MainTex);
            UNITY_DECLARE_TEX2D(_MipTex);
            Texture2D _EncryptTex;
            int _Key0;
            int _Key1;
            int _Key2;
            int _Key3;
";

            switch (ShaderManager.GetInstance().GetShaderType(shader))
            {
                case 7:
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
                        {
                            shader_code = shader_code_nofilter_XXTEA;
                        }
                        else if (filter == 1)
                        {
                            shader_code = shader_code_bilinear_XXTEA;
                        }
                        frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code);
                        frag = Regex.Replace(frag, "float4 mip_texture = _MipTex.Sample\\(sampler_MipTex, .*?\\);", "float4 mip_texture = _MipTex.Sample(sampler_MipTex, poiMesh.uv[0]);");
                        if (tex.format == TextureFormat.DXT1)
                        {
                            frag = Regex.Replace(frag, "DecryptTextureXXTEA", "DecryptTextureXXTEADXT1");
                        }
                        else if (EncryptTexture.HasAlpha(tex))
                        {
                            frag = Regex.Replace(frag, "DecryptTextureXXTEA", "DecryptTextureXXTEARGBA");
                        }
                        File.WriteAllText(path, frag);
                        break;
                    }
                case 8:
                    {
                        shader_data = Regex.Replace(shader_data, "UNITY_DECLARE_TEX2D\\(_MainTex\\);", declare);

                        shader_data = Regex.Replace(shader_data, "POI2D_SAMPLER_PAN\\((.*?), _MainTex", "POI2D_SAMPLER_PAN($1, _MipTex");
                        shader_data = Regex.Replace(shader_data, "UNITY_SAMPLE_TEX2D_SAMPLER_LOD\\((.*?), _MainTex", "UNITY_SAMPLE_TEX2D_SAMPLER_LOD($1, _MipTex");
                        shader_data = Regex.Replace(shader_data, "UNITY_SAMPLE_TEX2D_SAMPLER\\((.*?), _MainTex", "UNITY_SAMPLE_TEX2D_SAMPLER($1, _MipTex");
                        shader_data = Regex.Replace(shader_data, "float4 frag\\(", "#include \"Decrypt.cginc\"\n\t\t\tfloat4 frag(");
                        string shader_code = shader_code_nofilter_XXTEA;
                        if (filter == 0)
                        {
                            shader_code = shader_code_nofilter_XXTEA;
                        }
                        else if (filter == 1)
                        {
                            shader_code = shader_code_bilinear_XXTEA;
                        }
                        shader_data = Regex.Replace(shader_data, "float4 mainTexture = .*?;", shader_code);
                        if(tex.format == TextureFormat.DXT1 || tex.format == TextureFormat.DXT5)
                        {
                            shader_data = Regex.Replace(shader_data, "DecryptTextureXXTEA", "DecryptTextureXXTEADXT");
                        }
                        else if (EncryptTexture.HasAlpha(tex))
                        {
                            shader_data = Regex.Replace(shader_data, "DecryptTextureXXTEA", "DecryptTextureXXTEARGBA");
                        }
                        break;
                    }
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
    }
}
#endif