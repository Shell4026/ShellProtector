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
        readonly Dictionary<string, int> support_version = new Dictionary<string, int>();

        ushort[] keys = new ushort[8];
        int rounds = 0;
        int filter = 1;

        string shader_code_nofilter = @"
				float4 mip_texture = tex2D(_MipTex, poiMesh.uv[0]);
				
				int mip = round(mip_texture.a * 255 / 10); //fucking precision problems
				int m[13] = { 0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // max size 4k
				
				float4 c00 =  _MainTex.SampleLevel(sampler_MainTex, poiMesh.uv[0], m[mip]);
				c00 = DecryptTexture(c00, mainUV, m[mip]);

				float4 mainTexture = c00;
        ";

        string shader_code_bilinear = @"
				float4 mip_texture = tex2D(_MipTex, poiMesh.uv[0]);
				
				float2 uv_unit = _MainTex_TexelSize.xy;
				//bilinear interpolation
				float2 uv_bilinear = poiMesh.uv[0] - 0.5 * uv_unit;
				int mip = round(mip_texture.a * 255 / 10); //fucking precision problems
				int m[13] = { 0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // max size 4k
				
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

        public Injector(byte[] key, int rounds, int filter)
        {
            support_version.Add("Poiyomi 7.3", 7);
            support_version.Add("Poiyomi 8.0", 8);
            support_version.Add("Poiyomi 8.1", 8);
            support_version.Add("Poiyomi 8.2", 8);
            Init(key, rounds, filter);
        }

        public static bool IsPoiyomi(Shader shader)
        {
            if (shader.name.Contains("Poiyomi"))
                return true;
            return false;
        }

        private int GetSupportShaderType(Shader shader)
        {
            foreach (var version in support_version)
            {
                if (shader.name.Contains(version.Key))
                    return support_version[version.Key];
            }
            return -1;
        }

        public bool IsSupportShader(Shader shader)
        {
            foreach (var version in support_version)
            {
                if (shader.name.Contains(version.Key))
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

        public void Init(byte[] key, int rounds, int filter)
        {
            if (key.Length != 16)
            {
                Debug.LogError("Key bytes requires 16 byte");
                return;
            }
            for (int i = 0, j = 0; i < keys.Length; ++i, j += 2)
            {
                keys[i] = (ushort)(key[j] | key[j + 1] << 8);
            }
            this.rounds = rounds;
            this.filter = filter;
        }

        private string GenerateDecoder(string data, Texture2D tex)
        {
            if (data.Contains("//ShellProtect"))
            {
                Debug.LogWarning("The shader is already encrypted.");
                return null;
            }
            data.Insert(0, "//ShellProtect");

            string ks = "static uint mw[12] = { 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };";
            
            int mip_lv = tex.mipmapCount;
            string replace_mw = "static uint mw[" + (mip_lv + 2) + "] = { ";
            string replace_mh = "static uint mh[" + (mip_lv + 2) + "] = { ";
            for (int i = 0; i < mip_lv + 2; ++i)
            {
                replace_mw += tex.width / Mathf.Pow(2, i);
                replace_mh += tex.height / Mathf.Pow(2, i);
                if (i != mip_lv + 2 - 1)
                {
                    replace_mw += ", ";
                    replace_mh += ", ";
                }
                else
                {
                    replace_mw += " };";
                    replace_mh += " };";
                }
            }

            data = Regex.Replace(data, "static uint mw\\[12\\] = { 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };", replace_mw);
            data = Regex.Replace(data, "static uint mh\\[12\\] = { 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };", replace_mh);

            ks = "static uint k[8] = { " + keys[0] + ", " + keys[1] + ", " + keys[2] + ", " + keys[3] + ", " + keys[4] + ", " + keys[5] + ", 0, 0 };";
            data = Regex.Replace(data, "static uint k\\[8\\] = { 0, 0, 0, 0, 0, 0, 0, 0 };", ks);

            ks = "static const uint rounds = " + rounds;
            data = Regex.Replace(data, "static const uint rounds = 32", ks);

            return data;
        }

        public void Inject(Shader shader, string decode_dir, Texture2D tex)
        {
            string shader_path = AssetDatabase.GetAssetPath(shader);

            if (!File.Exists(decode_dir))
            {
                Debug.LogError(decode_dir + " is not exits.");
                return;
            }
            string decode_data = File.ReadAllText(decode_dir);
            decode_data = GenerateDecoder(decode_data, tex);
            if (decode_data == null)
                return;
            File.WriteAllText(Path.GetDirectoryName(shader_path) + "/Decrypt.cginc", decode_data);


            string shader_data = File.ReadAllText(shader_path);
            Match match = Regex.Match(shader_data, "Properties\\W*{");
            if (match.Success)
            {
                int suffix_idx = match.Index + match.Length;
                shader_data = shader_data.Insert(suffix_idx, "\n\t\t[HideInInspector] _MipTex (\"Texture\", 2D) = \"white\" { }");
            }
            else
            {
                Debug.LogError("Wrong shader data!");
                return;
            }

            switch (GetSupportShaderType(shader))
            {
                case -1:
                    {
                        break;
                    }
                case 7:
                    {
                        string frag_path = Path.GetDirectoryName(shader_path) + "/CGI_PoiFrag.cginc";
                        string frag = File.ReadAllText(frag_path);
                        frag = Regex.Replace(frag, "float4 frag\\(", "sampler2D _MipTex;\n#include \"Decrypt.cginc\"\nfloat4 frag(");
                        if (filter == 0)
                            frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code_nofilter);
                        else if (filter == 1)
                            frag = Regex.Replace(frag, "float4 mainTexture = .*?;", shader_code_bilinear);
                        File.WriteAllText(frag_path, frag);
                        break;
                    }
                case 8:
                    {
                        shader_data = Regex.Replace(shader_data, "float4 frag\\(", "sampler2D _MipTex;\n\t\t\t#include \"Decrypt.cginc\"\n\t\t\tfloat4 frag(");
                        if (filter == 0)
                            shader_data = Regex.Replace(shader_data, "float4 mainTexture = .*?;", shader_code_nofilter);
                        else if (filter == 1)
                            shader_data = Regex.Replace(shader_data, "float4 mainTexture = .*?;", shader_code_bilinear);
                        break;
                    }
            }
            File.WriteAllText(shader_path, shader_data);
            AssetDatabase.Refresh();
        }
    }
}