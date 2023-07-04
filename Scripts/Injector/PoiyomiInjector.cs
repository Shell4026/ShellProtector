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

            shader_data = Regex.Replace(shader_data, "Shader \"(.*?)\"", "Shader \"$1_encrypted\"");

            Match match = Regex.Match(shader_data, "Properties\\W*{");
            if (match.Success)
            {
                int suffix_idx = match.Index + match.Length;
                shader_data = shader_data.Insert(suffix_idx, "\n\t\t[HideInInspector] _MipTex (\"Texture\", 2D) = \"white\" { }");
            }
            else
            {
                Debug.LogError("Wrong shader data!");
                return null;
            }

            switch (ShaderManager.GetInstance().GetShaderType(shader))
            {
                case 7:
                    {
                        string frag_path = output_path + "/CGI_PoiFrag.cginc";
                        string frag = File.ReadAllText(frag_path);
                        frag = Regex.Replace(frag, "float4 frag\\(", "sampler2D _MipTex;\n#include \"Decrypt.cginc\"\nfloat4 frag(");
                        if (filter == 0)
                        {
                            frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code_nofilter_XXTEA);
                        }
                        else if (filter == 1)
                        {
                            frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code_bilinear_XXTEA);
                        }
                        File.WriteAllText(frag_path, frag);
                        break;
                    }
                case 8:
                    {
                        shader_data = Regex.Replace(shader_data, "float4 frag\\(", "sampler2D _MipTex;\n\t\t\t#include \"Decrypt.cginc\"\n\t\t\tfloat4 frag(");
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
                        if (EncryptTexture.HasAlpha(tex))
                            shader_data = Regex.Replace(shader_data, "DecryptTextureXXTEA", "DecryptTextureXXTEARGBA");
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