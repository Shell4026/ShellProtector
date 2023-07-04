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
        public override Shader Inject(Material mat, string decode_dir, Texture2D tex, bool xxtea)
        {
            if (!File.Exists(decode_dir))
            {
                Debug.LogError(decode_dir + " is not exits.");
                return null;
            }
            Shader shader = mat.shader;

            string shader_dir = AssetDatabase.GetAssetPath(shader);
            string shader_name = Path.GetFileNameWithoutExtension(shader_dir);
            string shader_folder = Path.GetDirectoryName(shader_dir);

            string shader_data;
            string output_dir = asset_dir + '/' + target.name + "/shader";

            if (!File.Exists(output_dir + "/lilCustomShaderDatas.lilblock"))
            {
                string[] files = Directory.GetFiles(asset_dir + "/lilToon/Shaders/");
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

                string decode = GenerateDecoder(decode_dir, tex, xxtea);
                File.WriteAllText(output_dir + "/Decrypt.cginc", decode);
            }
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