using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace Shell.Protector
{
    abstract public class Injector
    {
        protected ushort[] keys = new ushort[8]; //16byte
        protected ShaderManager shader_manager = ShaderManager.GetInstance();
        protected int rounds = 0;
        protected int filter = 1;
        protected string asset_dir;

        protected string shader_code_nofilter = @"
				float4 mip_texture = tex2D(_MipTex, poiMesh.uv[0]);
				
				int mip = round(mip_texture.a * 255 / 10); //fucking precision problems
				int m[13] = { 0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // max size 4k
				
				float4 c00 =  _MainTex.SampleLevel(sampler_MainTex, poiMesh.uv[0], m[mip]);
				c00 = DecryptTexture(c00, mainUV, m[mip]);

				float4 mainTexture = c00;
        ";
        protected string shader_code_nofilter_XXTEA = @"
				float4 mip_texture = tex2D(_MipTex, poiMesh.uv[0]);
				
				int mip = round(mip_texture.a * 255 / 10); //fucking precision problems
				int m[13] = { 0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // max size 4k

				float4 c00 = float4(DecryptTextureXXTEA(mainUV, m[mip]), 1.0);

				float4 mainTexture = c00;
        ";
        protected string shader_code_bilinear = @"
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
        protected string shader_code_bilinear_XXTEA = @"
				float4 mip_texture = tex2D(_MipTex, poiMesh.uv[0]);
				
				float2 uv_unit = _MainTex_TexelSize.xy;
				//bilinear interpolation
				float2 uv_bilinear = poiMesh.uv[0] - 0.5 * uv_unit;
				int mip = round(mip_texture.a * 255 / 10); //fucking precision problems
				int m[13] = { 0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // max size 4k
				
                float4 c00 = float4(DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 0), m[mip]), 1.0);
                float4 c10 = float4(DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 0), m[mip]), 1.0);
                float4 c01 = float4(DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 1), m[mip]), 1.0);
                float4 c11 = float4(DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 1), m[mip]), 1.0);
				
				float2 f = frac(uv_bilinear * _MainTex_TexelSize.zw);
				
				float4 c0 = lerp(c00, c10, f.x);
				float4 c1 = lerp(c01, c11, f.x);

				float4 bilinear = lerp(c0, c1, f.y);
				
				float4 mainTexture = bilinear;
        ";

        public void Init(byte[] key, int filter, string asset_dir, int rounds = 32)
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
            this.asset_dir = asset_dir;
        }

        protected string GenerateDecoder(string data, Texture2D tex, bool xxtea)
        {
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
            string ks;

            data = Regex.Replace(data, "static uint mw\\[12\\] = { 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };", replace_mw);
            data = Regex.Replace(data, "static uint mh\\[12\\] = { 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };", replace_mh);

            if (!xxtea)
            {
                ks = "static uint k[8] = { " + keys[0] + ", " + keys[1] + ", " + keys[2] + ", " + keys[3] + ", " + keys[4] + ", " + keys[5] + ", 0, 0 };";
                data = Regex.Replace(data, "static uint k\\[8\\] = { 0, 0, 0, 0, 0, 0, 0, 0 };", ks);
            }
            else
            {
                ks = "static uint k[4] = { " + (keys[0] + (keys[1] << 16)) + ", " + (keys[2] + (keys[3] << 16)) + ", " + (keys[4] + (keys[5] << 16)) + ", 0 };";
                data = Regex.Replace(data, "static uint k\\[8\\] = { 0, 0, 0, 0, 0, 0, 0, 0 };", ks);
            }
            ks = "static const uint rounds = " + rounds;
            data = Regex.Replace(data, "static const uint rounds = 32", ks);

            return data;
        }

        public bool WasInjected(Shader shader)
        {
            string shader_path = AssetDatabase.GetAssetPath(shader);

            string shader_data = File.ReadAllText(shader_path);
            if (shader_data.Contains("//ShellProtect"))
                return true;
            return false;
        }

        abstract public Shader Inject(Shader shader, string decode_dir, Texture2D tex, bool xxtea);
    /*else if(shader_manager.IslilToon(shader))
{
    string name = AssetDatabase.GetAssetPath(shader);
    string[] path_segment = name.Split('/');
    name = path_segment[path_segment.Length - 1];
    path_segment = name.Split('.');
    name = path_segment[path_segment.Length - 2];
    return_shader = AssetDatabase.LoadAssetAtPath(asset_dir + "/liltoon/Shaders/" + name + ".lilcontainer", typeof(Shader)) as Shader;
}*/
}
}
#endif