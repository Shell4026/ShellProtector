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
        public override Shader Inject(Shader shader, string decode_dir, Texture2D tex, bool xxtea)
        {
            if (!File.Exists(decode_dir))
            {
                Debug.LogError(decode_dir + " is not exits.");
                return null;
            }

            string shader_path = AssetDatabase.GetAssetPath(shader);
            string shader_data = File.ReadAllText(shader_path); ;

            shader_data = shader_data.Insert(0, "//ShellProtect\n");

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
                        string frag_path = Path.GetDirectoryName(shader_path) + "/CGI_PoiFrag.cginc";
                        string frag = File.ReadAllText(frag_path);
                        frag = Regex.Replace(frag, "float4 frag\\(", "sampler2D _MipTex;\n#include \"Decrypt.cginc\"\nfloat4 frag(");
                        if (filter == 0)
                        {
                            if (!xxtea)
                                frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code_nofilter);
                            else
                                frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code_nofilter_XXTEA);
                        }
                        else if (filter == 1)
                        {
                            if (!xxtea)
                                frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code_bilinear);
                            else
                                frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code_bilinear_XXTEA);
                        }
                        File.WriteAllText(frag_path, frag);
                        break;
                    }
                case 8:
                    {
                        shader_data = Regex.Replace(shader_data, "float4 frag\\(", "sampler2D _MipTex;\n\t\t\t#include \"Decrypt.cginc\"\n\t\t\tfloat4 frag(");
                        string shader_code = shader_code_nofilter;
                        if (filter == 0)
                        {
                            if (!xxtea)
                                shader_code = shader_code_nofilter;
                            else
                                shader_code = shader_code_nofilter_XXTEA;
                        }
                        else if (filter == 1)
                        {
                            if (!xxtea)
                                shader_code = shader_code_bilinear;
                            else
                                shader_code = shader_code_bilinear_XXTEA;
                        }
                        shader_data = Regex.Replace(shader_data, "float4 mainTexture = .*?;", shader_code);
                        if (EncryptTexture.HasAlpha(tex) && xxtea)
                            shader_data = Regex.Replace(shader_data, "DecryptTextureXXTEA", "DecryptTextureXXTEARGBA");
                        break;
                    }
            }
            File.WriteAllText(shader_path, shader_data);

            string decode_data = File.ReadAllText(decode_dir);
            decode_data = GenerateDecoder(decode_data, tex, xxtea);
            if (decode_data == null)
            {
                Debug.LogError("Can't generate decode.cginc");
                return null;
            }

            File.WriteAllText(Path.GetDirectoryName(shader_path) + "/Decrypt.cginc", decode_data);
            AssetDatabase.Refresh();
            return shader;
        }
    }
}
#endif