﻿#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    public class LilToonInjector : Injector
    {
        string output_dir;
        string shaderCodePoint = @"\
		    half4 mip_texture = tex2D(_MipTex, fd.uvMain);\
			int mip = round(mip_texture.r * 255 / 10);\
			int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
\
			half4 c00 = DecryptTexture(fd.uvMain, m[mip]);\
\
		    fd.col = c00;\
";
        string shaderCodeBilinear = @"\
			half4 mip_texture = tex2D(_MipTex, fd.uvMain);\
				\
			half2 uv_unit = _EncryptTex0_TexelSize.xy;\
			half2 uv_bilinear = fd.uvMain - 0.5 * uv_unit;\
			int mip = round(mip_texture.r * 255 / 10);\
			int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
				\
            half4 c00 = DecryptTexture(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);\
            half4 c10 = DecryptTexture(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);\
            half4 c01 = DecryptTexture(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);\
            half4 c11 = DecryptTexture(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);\
				\
			half2 f = frac(uv_bilinear * _EncryptTex0_TexelSize.zw);\
				\
			half4 c0 = lerp(c00, c10, f.x);\
			half4 c1 = lerp(c01, c11, f.x);\
\
			half4 bilinear = lerp(c0, c1, f.y);\
				\
			fd.col = bilinear;\
";

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

            Decoder decoder = GenerateDecoder(decode_dir, tex);
            File.WriteAllText(Path.Combine(output_dir, "Decrypt.cginc"), decoder.decrypt);
            if (decoder.xxtea != null)
                File.WriteAllText(Path.Combine(output_path, "XXTEA.cginc"), decoder.xxtea);
            else if (decoder.chacha != null)
                File.WriteAllText(Path.Combine(output_path, "Chacha.cginc"), decoder.chacha);

            shader_data = File.ReadAllText(Path.Combine(output_dir, shader_name + ".lilcontainer"));
            shader_data.Insert(0, "//ShellProtect\n");
            File.WriteAllText(Path.Combine(output_dir, shader_name + ".lilcontainer"), shader_data);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Shader new_shader = AssetDatabase.LoadAssetAtPath(Path.Combine(output_dir, shader_name + ".lilcontainer"), typeof(Shader)) as Shader;
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

            string injectCode = "";
            if (filter == 0)
                injectCode = shaderCodePoint;
            else if (filter == 1)
                injectCode = shaderCodeBilinear;

            if (tex.format == TextureFormat.DXT1 || tex.format == TextureFormat.DXT5)
                injectCode = Regex.Replace(injectCode, "DecryptTexture", "DecryptTextureDXT");
            else
            {
                if (EncryptTexture.HasAlpha(tex))
                    injectCode = Regex.Replace(injectCode, "DecryptTexture", "DecryptTextureRGBA");
            }

            custom = Regex.Replace(custom, "//inject", injectCode);

            File.WriteAllText(output_dir + "/custom.hlsl", custom);
        }
        private void ChangeShaderName()
        {
            string shader_data = File.ReadAllText(Path.Combine(output_dir, "lilCustomShaderDatas.lilblock"));
            shader_data = shader_data.Replace("ShaderName \"hidden/ShellProtector\"", "ShaderName \"hidden/ShellProtector_" + main_tex.GetInstanceID() + "\"");
            File.WriteAllText(output_dir + "/lilCustomShaderDatas.lilblock", shader_data);
        }
    }
}
#endif