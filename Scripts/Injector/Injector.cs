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
        protected int filter = 1;
        protected string asset_dir;

        protected string shader_code_nofilter_XXTEA = @"
				float4 mip_texture = _MipTex.Sample(sampler_MipTex, mainUV);
				
				int mip = round(mip_texture.r * 255 / 10); //fucking precision problems
				int m[13] = { 0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // max size 4k

				float4 c00 = DecryptTextureXXTEA(mainUV, m[mip]);

				float4 mainTexture = c00;
        ";
        protected string shader_code_bilinear_XXTEA = @"
				float4 mip_texture = _MipTex.Sample(sampler_MipTex, mainUV);
				
				float2 uv_unit = _MainTex_TexelSize.xy;
				//bilinear interpolation
				float2 uv_bilinear = poiMesh.uv[0] - 0.5 * uv_unit;
				int mip = round(mip_texture.r * 255 / 10); //fucking precision problems
				int m[13] = { 0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // max size 4k
				
                float4 c00 = DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);
                float4 c10 = DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);
                float4 c01 = DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);
                float4 c11 = DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);
				
				float2 f = frac(uv_bilinear * _MainTex_TexelSize.zw);
				
				float4 c0 = lerp(c00, c10, f.x);
				float4 c1 = lerp(c01, c11, f.x);

				float4 bilinear = lerp(c0, c1, f.y);
				
				float4 mainTexture = bilinear;
        ";

        protected GameObject target;

        public void Init(GameObject target, byte[] key, int filter, string asset_dir, int rounds = 32)
        {
            if (key.Length != 16)
            {
                Debug.LogError("Key bytes requires 16 byte");
                return;
            }
            this.target = target;
            for (int i = 0, j = 0; i < keys.Length; ++i, j += 2)
            {
                keys[i] = (ushort)(key[j] | key[j + 1] << 8);
            }
            this.filter = filter;
            this.asset_dir = asset_dir;
        }

        protected string GenerateDecoder(string decode_dir, Texture2D tex)
        {
            string data = File.ReadAllText(decode_dir);
            if (data == null)
            {
                Debug.LogError("Can't read decode.cginc");
                return null;
            }
            string replace;

            uint k0 = (uint)(keys[0] + (keys[1] << 16));
            uint k1 = (uint)(keys[2] + (keys[3] << 16));
            uint k2 = (uint)(keys[4] + (keys[5] << 16));
            //uint k3 = (uint)(keys[6] + (keys[7] << 16));
            replace = "static const uint k[3] = { " + k0 + ", " + k1 + ", " + k2 + " };";
            data = Regex.Replace(data, "static const uint k\\[3\\] = { 0, 0, 0 };", replace);
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

        abstract public Shader Inject(Material mat, string decode_dir, Texture2D tex);
    }
}
#endif