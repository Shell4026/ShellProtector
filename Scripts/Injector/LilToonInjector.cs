#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    public class LilToonInjector : Injector
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

            string shader_dir = AssetDatabase.GetAssetPath(shader);
            string shader_name = Path.GetFileNameWithoutExtension(shader_dir);
            string shader_folder = Path.GetDirectoryName(shader_dir);

            string shader_data;
            string output_dir = asset_dir + '/' + target.name + "/shader/" + mat.name;

            string[] files = Directory.GetFiles(asset_dir + "/lilToonCustom/Shaders/");
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Contains(".meta"))
                    continue;
                File.Copy(file, Path.Combine(output_dir, filename), true);
            }
            shader_data = File.ReadAllText(output_dir + "/lilCustomShaderDatas.lilblock");
            shader_data = shader_data.Replace("ShaderName \"hidden/ShellProtector\"", "ShaderName \"hidden/ShellProtector_" + target.GetInstanceID() + "\"");
            File.WriteAllText(output_dir + "/lilCustomShaderDatas.lilblock", shader_data);

            shader_data = File.ReadAllText(output_dir + "/custom.hlsl");
            int code = 0;

            if (filter == 0)
            {
                if (tex.format == TextureFormat.DXT1)
                    code = 5;
                else
                {
                    code = 2;
                    if (EncryptTexture.HasAlpha(tex))
                        code = 3;
                }
            }
            else if (filter == 1)
            {
                if (tex.format == TextureFormat.DXT1)
                    code = 4;
                else
                {
                    code = 0;
                    if (EncryptTexture.HasAlpha(tex))
                        code = 1;
                }
            }
            shader_data = Regex.Replace(shader_data, "const int code = 0;", "const int code = " + code + ";");
            File.WriteAllText(output_dir + "/custom.hlsl", shader_data);

            string decode = GenerateDecoder(decode_dir, tex);
            File.WriteAllText(output_dir + "/Decrypt.cginc", decode);

            shader_data = File.ReadAllText(output_dir + "/" + shader_name + ".lilcontainer");
            shader_data.Insert(0, "//ShellProtect");
            File.WriteAllText(output_dir + "/" + shader_name + ".lilcontainer", shader_data);
            AssetDatabase.Refresh();

            Shader new_shader = AssetDatabase.LoadAssetAtPath(output_dir + "/" + shader_name + ".lilcontainer", typeof(Shader)) as Shader;
            return new_shader;
        }
    }
}
#endif