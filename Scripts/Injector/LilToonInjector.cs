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
        string output_dir;
        public override Shader Inject(Material mat, string decode_dir, string output_path, Texture2D tex, bool has_lim_texture = false, bool has_lim_texture2 = false, bool outline_tex = false)
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

            string shader_dir = AssetDatabase.GetAssetPath(shader);
            string shader_name = Path.GetFileNameWithoutExtension(shader_dir);
            string shader_folder = Path.GetDirectoryName(shader_dir);

            string shader_data;
            output_dir = output_path;

            CopyShaderFiles(shader_name);

            ProcessCustom(tex, has_lim_texture, outline_tex);

            ChangeShaderName();

            string decode = GenerateDecoder(decode_dir, tex);
            File.WriteAllText(output_dir + "/Decrypt.cginc", decode);

            shader_data = File.ReadAllText(Path.Combine(output_dir, shader_name + ".lilcontainer"));
            shader_data.Insert(0, "//ShellProtect\n");
            File.WriteAllText(Path.Combine(output_dir, shader_name + ".lilcontainer"), shader_data);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Shader new_shader = AssetDatabase.LoadAssetAtPath(output_dir + "/" + shader_name + ".lilcontainer", typeof(Shader)) as Shader;
            return new_shader;
        }
        private void CopyShaderFiles(string original_shader_name)
        {
            string[] files = Directory.GetFiles(Path.Combine(asset_dir, "lilToonCustom", "Shaders"));
            string pass = "qwerty";
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Contains(original_shader_name))
                {
                    string f = File.ReadAllText(file);
                    Match match = Regex.Match(f, "lilPassShaderName \".*/(.*?)\"");
                    if (match.Success)
                        pass = match.Groups[1].Value;
                    break;
                }
            }
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Contains(".meta"))
                    continue;
                if (filename.Contains(".lilcontainer"))
                {
                    if (filename != original_shader_name + ".lilcontainer" && filename != pass + ".lilcontainer")
                        continue;
                }
                File.Copy(file, Path.Combine(output_dir, filename), true);
            }
        }
        private void ProcessCustom(Texture2D tex, bool lim, bool outline)
        {
            string path = Path.Combine(output_dir, "custom.hlsl");
            string custom = File.ReadAllText(path);
            if(outline)
                custom = custom.Insert(0, "#define OUTLINE_ENCRYPTED\n");
            if(lim)
                custom = custom.Insert(0, "#define LIMLIGHT_ENCRYPTED\n");

            int code = 0;
            if (filter == 0)
            {
                if (tex.format == TextureFormat.DXT1 || tex.format == TextureFormat.DXT5)
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
                if (tex.format == TextureFormat.DXT1 || tex.format == TextureFormat.DXT5)
                    code = 4;
                else
                {
                    code = 0;
                    if (EncryptTexture.HasAlpha(tex))
                        code = 1;
                }
            }
            custom = Regex.Replace(custom, "const int code = 0;", "const int code = " + code + ";");

            File.WriteAllText(output_dir + "/custom.hlsl", custom);
        }
        private void ChangeShaderName()
        {
            string shader_data = File.ReadAllText(Path.Combine(output_dir, "lilCustomShaderDatas.lilblock"));
            shader_data = shader_data.Replace("ShaderName \"hidden/ShellProtector\"", "ShaderName \"hidden/ShellProtector_" + target.GetInstanceID() + "\"");
            File.WriteAllText(output_dir + "/lilCustomShaderDatas.lilblock", shader_data);
        }
    }
}
#endif