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
        protected AssetManager shader_manager = AssetManager.GetInstance();
        protected int filter = 1;
        protected string asset_dir;
        protected int user_key_length = 4;
        protected uint rounds = 25;

        protected string shader_code_nofilter_XXTEA = @"
				half4 mip_texture = _MipTex.Sample(sampler_MipTex, mainUV);
				
				int mip = round(mip_texture.r * 255 / 10); //fucking precision problems
				int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 }; // max size 4k

				half4 c00 = DecryptTextureXXTEA(mainUV, m[mip]);

				half4 mainTexture = c00;
        ";
        protected string shader_code_bilinear_XXTEA = @"
				half4 mip_texture = _MipTex.Sample(sampler_MipTex, mainUV);
				
				half2 uv_unit = _MainTex_TexelSize.xy;
				//bilinear interpolation
				half2 uv_bilinear = poiMesh.uv[0] - 0.5 * uv_unit;
				int mip = round(mip_texture.r * 255 / 10); //fucking precision problems
				int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 }; // max size 4k
				
                half4 c00 = DecryptTextureXXTEA(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);
                half4 c10 = DecryptTextureXXTEA(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);
                half4 c01 = DecryptTextureXXTEA(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);
                half4 c11 = DecryptTextureXXTEA(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);
				
				half2 f = frac(uv_bilinear * _MainTex_TexelSize.zw);
				
				half4 c0 = lerp(c00, c10, f.x);
				half4 c1 = lerp(c01, c11, f.x);

				half4 bilinear = lerp(c0, c1, f.y);
				
				half4 mainTexture = bilinear;
        ";

        protected GameObject target;
        protected Texture2D main_tex;

        public void Init(GameObject target, Texture2D main_tex, byte[] key, int user_key_length, int filter, string asset_dir, uint rounds)
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
            this.main_tex = main_tex;
            this.filter = filter;
            this.asset_dir = asset_dir;
            this.user_key_length = user_key_length;
            this.rounds = rounds;
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
            string replace2;
            uint k0 = (uint)(keys[0] + (keys[1] << 16));
            uint k1 = (uint)(keys[2] + (keys[3] << 16));
            uint k2 = (uint)(keys[4] + (keys[5] << 16));
            uint k3 = (uint)(keys[6] + (keys[7] << 16));
            //uint k3 = (uint)(keys[6] + (keys[7] << 16));
            switch(user_key_length)
            {
                case 0:
                    replace = "static const uint k[4] = { " + k0 + ", " + k1 + ", " + k2 + ", " + k3 + " };";

                    replace2 = @"
	uint key[4];
	key[0] = k[0];
	key[1] = k[1];
	key[2] = k[2];
	key[3] = k[3] ^ (uint)(floor(idx / 2) * 2);";
                    break;
                case 4:
                    replace = "static const uint k[4] = { " + k0 + ", " + k1 + ", " + k2 + ", 0 };\n";
                    replace += "float _Key0, _Key1, _Key2, _Key3;";

                    replace2 = @"
    uint key0 = round(_Key0);
    uint key1 = round(_Key1);
    uint key2 = round(_Key2);
    uint key3 = round(_Key3);

	uint key[4];
	key[0] = k[0];
	key[1] = k[1];
	key[2] = k[2];
	key[3] = ((uint)(key0) | (uint)(key1 << 8) | (uint)(key2 << 16) | (uint)(key3 << 24)) ^ (uint)(floor(idx / 2) * 2);";
                    break;
                case 8:
                    replace = "static const uint k[4] = { " + k0 + ", " + k1 + ", 0, 0 };\n";
                    replace += "float _Key0, _Key1, _Key2, _Key3, _Key4, _Key5, _Key6, _Key7;";

                    replace2 = @"
    uint key0 = round(_Key0);
    uint key1 = round(_Key1);
    uint key2 = round(_Key2);
    uint key3 = round(_Key3);
    uint key4 = round(_Key4);
    uint key5 = round(_Key5);
    uint key6 = round(_Key6);
    uint key7 = round(_Key7);

	uint key[4];
	key[0] = k[0];
	key[1] = k[1];
	key[2] = ((uint)(key0) | (uint)(key1 << 8) | (uint)(key2 << 16) | (uint)(key3 << 24));
	key[3] = ((uint)(key4) | (uint)(key5 << 8) | (uint)(key6 << 16) | (uint)(key7 << 24)) ^ (uint)((idx >> 1) << 1);";
                    break;
                case 12:
                    replace = "static const uint k[4] = { " + k0 + ", 0, 0, 0 };\n";
                    replace += "float _Key0, _Key1, _Key2, _Key3, _Key4, _Key5, _Key6, _Key7, _Key8, _Key9, _Key10, _Key11;";

                    replace2 = @"
    uint key0 = round(_Key0);
    uint key1 = round(_Key1);
    uint key2 = round(_Key2);
    uint key3 = round(_Key3);
    uint key4 = round(_Key4);
    uint key5 = round(_Key5);
    uint key6 = round(_Key6);
    uint key7 = round(_Key7);
    uint key8 = round(_Key8);
    uint key9 = round(_Key9);
    uint key10 = round(_Key10);
    uint key11 = round(_Key11);

	uint key[4];
	key[0] = k[0];
	key[1] = ((uint)(key0) | (uint)(key1 << 8) | (uint)(key2 << 16) | (uint)(key3 << 24));
	key[2] = ((uint)(key4) | (uint)(key5 << 8) | (uint)(key6 << 16) | (uint)(key7 << 24));
	key[3] = ((uint)(key8) | (uint)(key9 << 8) | (uint)(key10 << 16) | (uint)(key11 << 24)) ^ (uint)((idx >> 1) << 1);";
                    break;
                default:
                    replace = "static const uint k[4] = { 0, 0, 0, 0 };\n";
                    replace += "float _Key0, _Key1, _Key2, _Key3, _Key4, _Key5, _Key6, _Key7, _Key8, _Key9, _Key10, _Key11, _Key12, _Key13, _Key14, _Key15;";

                    replace2 = @"
    uint key0 = round(_Key0);
    uint key1 = round(_Key1);
    uint key2 = round(_Key2);
    uint key3 = round(_Key3);
    uint key4 = round(_Key4);
    uint key5 = round(_Key5);
    uint key6 = round(_Key6);
    uint key7 = round(_Key7);
    uint key8 = round(_Key8);
    uint key9 = round(_Key9);
    uint key10 = round(_Key10);
    uint key11 = round(_Key11);
    uint key12 = round(_Key12);
    uint key13 = round(_Key13);
    uint key14 = round(_Key14);
    uint key15 = round(_Key15);

	uint key[4];
	key[0] = ((uint)(key0) | (uint)(key1 << 8) | (uint)(key2 << 16) | (uint)(key3 << 24));
	key[1] = ((uint)(key4) | (uint)(key5 << 8) | (uint)(key6 << 16) | (uint)(key7 << 24));
	key[2] = ((uint)(key8) | (uint)(key9 << 8) | (uint)(key10 << 16) | (uint)(key11 << 24));
	key[3] = ((uint)(key12) | (uint)(key13 << 8) | (uint)(key14 << 16) | (uint)(key15 << 24)) ^ (uint)((idx >> 1) << 1);";
                    break;
            }

            if(main_tex != null)
            {
                if(main_tex.format == TextureFormat.DXT1 || main_tex.format == TextureFormat.DXT5)
                {
                    int woffset = 13 - (int)Mathf.Log(main_tex.width, 2) -1 + 2;
                    int hoffset = 13 - (int)Mathf.Log(main_tex.height, 2) - 1 + 2;
                    data = Regex.Replace(data, "static const uint WOFFSET = 0;", "static const uint WOFFSET = " + woffset + ";");
                    data = Regex.Replace(data, "static const uint HOFFSET = 0;", "static const uint HOFFSET = " + hoffset + ";");
                }
                else
                {
                    int woffset = 13 - (int)Mathf.Log(main_tex.width, 2) - 1;
                    int hoffset = 13 - (int)Mathf.Log(main_tex.height, 2) - 1;
                    data = Regex.Replace(data, "static const uint WOFFSET = 0;", "static const uint WOFFSET = " + woffset + ";");
                    data = Regex.Replace(data, "static const uint HOFFSET = 0;", "static const uint HOFFSET = " + hoffset + ";");
                }
            }
                

            data = Regex.Replace(data, "static const uint k\\[4\\] = { 0, 0, 0, 0 };", replace);
            data = Regex.Replace(data, "static const uint ROUNDS = 6;", "static const uint ROUNDS = " + rounds + ";");
            data = Regex.Replace(data, "//key make[\\w\\W]*?//key make end", replace2);
            data = Regex.Replace(data, @"key\[3\] = (.*?)\(uint\)\(floor\(idx / 2\) \* 2\);[\w\W]*?//4idx", "key[3] = $1(uint)((idx >> 2) << 2);"); //DecryptRGB
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

        abstract public Shader Inject(Material mat, string decode_dir, string output_dir, Texture2D tex, bool has_lim_texture = false, bool has_lim_texture2 = false, bool outline_tex = false);
    }
}
#endif