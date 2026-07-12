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
        public override bool CanHandle(Shader shader)
        {
            return ShaderManager.IsLilToon(shader);
        }

        protected override Shader CustomInject(Material mat, string decodeDir, string outputPath, Texture2D tex, bool hasLimTexture = false, bool hasLimTexture2 = false, bool outlineTex = false)
        {
            string[] files = Directory.GetFiles(Path.Combine(AssetDir, "liltoonProtector", "Shaders"));
            // Find pass
            string shaderDir = AssetDatabase.GetAssetPath(mat.shader);
            string shaderName = Path.GetFileNameWithoutExtension(shaderDir);
            string pass = "";
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Contains(shaderName))
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
                    if (filename == shaderName + ".lilcontainer")
                    {
                        Debug.Log(filename);
                        return AssetDatabase.LoadAssetAtPath<Shader>(Path.Combine(Path.Combine(AssetDir, "liltoonProtector", "Shaders"), filename));
                    }
                }
            }
            return null;
        }
    }
}
#endif
