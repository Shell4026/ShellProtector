using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace sh
{
    public class Injector
    {
        readonly static string[] support_version = { "Poiyomi 8.2" };

        string decoder = "";
        ushort[] keys = new ushort[8];
        int rounds = 0;

        string shader_code = @"
				float4 mip_texture = tex2D(_MipTex, mainUV);
				
				float2 uv_unit = _MainTex_TexelSize.xy;
				//bilinear interpolation
				float2 uv_bilinear = mainUV - 0.5 * uv_unit;
				int mip = round(mip_texture.a * 255 / 10);
				int m[12] = {0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9};
				
				float4 c00 =  _MainTex.SampleLevel(sampler_MainTex, uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);
				float4 c10 =  _MainTex.SampleLevel(sampler_MainTex, uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);
				float4 c01 =  _MainTex.SampleLevel(sampler_MainTex, uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);
				float4 c11 =  _MainTex.SampleLevel(sampler_MainTex, uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);
				
				c00 = DecryptTexture(c00, uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);
				c10 = DecryptTexture(c10, uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);
				c01 = DecryptTexture(c01, uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);
				c11 = DecryptTexture(c11, uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);
				
				float2 f = frac(uv_bilinear * _MainTex_TexelSize.zw);
				
				float4 c0 = lerp(c00, c10, f.x);
				float4 c1 = lerp(c01, c11, f.x);

				float4 bilinear = lerp(c0, c1, f.y);
				
				float4 mainTexture = bilinear;
        ";

        public Injector(byte[] key, int rounds)
        {
            Init(key, rounds);
        }

        public static bool IsPoiyomi(Shader shader)
        {
            if (shader.name.Contains("Poiyomi"))
                return true;
            return false;
        }
        public static bool IsSupportShader(Shader shader)
        {
            foreach (var version in support_version)
            {
                if (shader.name.Contains(version))
                    return true;
            }
            return false;
        }

        public static bool IsLockPoiyomi(Shader shader)
        {
            if (IsPoiyomi(shader))
            {
                if (shader.name.Contains("Locked"))
                    return true;
                return false;
            }
            return false;
        }

        public void Init(byte[] key, int rounds)
        {
            if (key.Length != 16)
            {
                Debug.LogWarning("Key bytes requires 16 byte");
                return;
            }
            for (int i = 0, j = 0; i < keys.Length; ++i, j += 2)
            {
                keys[i] = (ushort)(key[j] | key[j + 1] << 8);
            }
            this.rounds = rounds;
        }

        public void Inject(Shader shader, string decode_dir, Texture2D tex, Texture2D mip)
        {
            string shader_path = AssetDatabase.GetAssetPath(shader);

            string decode_data = File.ReadAllText(decode_dir);
            if (decode_data.Contains("//ShellProtect"))
            {
                Debug.LogWarning("The shader is already encrypted.");
                return;
            }
            decode_data.Insert(0, "//ShellProtect");

            string ks = "static uint k[8] = { " + keys[0] + ", " + keys[1] + ", " + keys[2] + ", " + keys[3] + ", " + keys[4] + ", " + keys[5] + ", 0, 0 };";
            decode_data = Regex.Replace(decode_data, "static uint k\\[8\\] = { 0, 0, 0, 0, 0, 0, 0, 0 };", ks);

            ks = "static const uint rounds = " + rounds;
            decode_data = Regex.Replace(decode_data, "static const uint rounds = 32", ks);

            File.WriteAllText(Path.GetDirectoryName(shader_path) + "/Decrypt.cginc", decode_data);

            string shader_data = File.ReadAllText(shader_path);

            Match match = Regex.Match(shader_data, "Properties\\W*{");
            if (match.Success)
            {
                int suffix_idx = match.Index + match.Length;
                shader_data = shader_data.Insert(suffix_idx, "\n\t\t[HideInInspector] _MipTex (\"Texture\", 2D) = \"white\" { }");

            }
            shader_data = Regex.Replace(shader_data, "float4 frag\\(", "sampler2D _MipTex;\n\t\t\t#include \"Decrypt.cginc\"\n\t\t\tfloat4 frag(");

            shader_data = Regex.Replace(shader_data, "float4 mainTexture = .*?;", shader_code);
            File.WriteAllText(shader_path, shader_data);

            AssetDatabase.Refresh();
        }
    }
}