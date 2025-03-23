#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    public class LilToonInjector : Injector
    {
        protected override Shader CustomInject(Material mat, string decode_dir, string output_path, Texture2D tex, bool has_lim_texture = false, bool has_lim_texture2 = false, bool outline_tex = false)
        {
            string[] files = Directory.GetFiles(Path.Combine(asset_dir, "liltoonProtector", "Shaders"));
            // Find pass
            string shader_dir = AssetDatabase.GetAssetPath(mat.shader);
            string shader_name = Path.GetFileNameWithoutExtension(shader_dir);
            string pass = "";
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Contains(shader_name))
                {
                    string f = File.ReadAllText(file);
                    Match match = Regex.Match(f, "lilPassShaderName \".*/(.*?)\"");
                    if (match.Success)
                        pass = match.Groups[1].Value;
                    break;
                }
            }
            // Select shader
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Contains(".lilcontainer"))
                {
                    if (filename == shader_name + ".lilcontainer")
                    {
                        Debug.Log(filename);
                        return AssetDatabase.LoadAssetAtPath<Shader>(Path.Combine(Path.Combine(asset_dir, "liltoonProtector", "Shaders"), filename));
                    }
                }
            }
            return null;
        }
    }
}
#endif