using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using static VRC.SDK3.Dynamics.PhysBone.PhysBoneMigration.DynamicBoneColliderData;

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

        protected string shader_code_nofilter = @"
				half4 mainTexture;

                UNITY_BRANCH
                if(_fallback == 0)
                {
				    half4 mip_texture = _MipTex.Sample(sampler_MipTex, mainUV);
				
				    int mip = round(mip_texture.r * 255 / 10); //fucking precision problems
				    const int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 }; // max size 4k

				    half4 c00 = DecryptTexture(mainUV, m[mip]);

				    mainTexture = c00;
                }
                else
                {
                    mainTexture = _MainTex.Sample(sampler_MainTex, mainUV);
                }
        ";
        protected string shader_code_bilinear = @"
                half4 mainTexture;

                UNITY_BRANCH
                if(_fallback == 0)
                {
				    half4 mip_texture = _MipTex.Sample(sampler_MipTex, mainUV);
				
				    half2 uv_unit = _EncryptTex0_TexelSize.xy;
				    //bilinear interpolation
				    half2 uv_bilinear = poiMesh.uv[0] - 0.5 * uv_unit;
				    int mip = round(mip_texture.r * 255 / 10); //fucking precision problems
				    const int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 }; // max size 4k
				
                    half4 c00 = DecryptTexture(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);
                    half4 c10 = DecryptTexture(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);
                    half4 c01 = DecryptTexture(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);
                    half4 c11 = DecryptTexture(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);
				
				    half2 f = frac(uv_bilinear * _EncryptTex0_TexelSize.zw);
				
				    half4 c0 = lerp(c00, c10, f.x);
				    half4 c1 = lerp(c01, c11, f.x);

				    half4 bilinear = lerp(c0, c1, f.y);
				
				    mainTexture = bilinear;
                }
                else
                {
                    mainTexture = _MainTex.Sample(sampler_MainTex, mainUV);
                }
        ";

        protected GameObject target;
        protected Texture2D main_tex;
        protected IEncryptor encryptor;

        protected struct Decoder
        {
            public Decoder(string decrypt = null, string xxtea = null, string chacha = null)
            {
                this.decrypt = decrypt;
                this.xxtea = xxtea;
                this.chacha = chacha;
            }
            public string decrypt;
            public string xxtea;
            public string chacha;
        }

        public void Init(GameObject target, Texture2D main_tex, byte[] key, int user_key_length, int filter, string asset_dir, IEncryptor encryptor)
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
            this.encryptor = encryptor;
        }

        public bool WasInjected(Shader shader)
        {
            string shader_path = AssetDatabase.GetAssetPath(shader);

            string shader_data = File.ReadAllText(shader_path);
            if (shader_data.Contains("//ShellProtect"))
                return true;
            return false;
        }

        private void SetKeywords(Material material, bool has_lim_texture = false) {
            // Clear keywords prefixed with _SHELL_PROTECTOR_
            var keywords = material.shaderKeywords;
            foreach (string keyword in keywords)
            {
                if (keyword.StartsWith("_SHELL_PROTECTOR_")) {
                    material.DisableKeyword(keyword);
                }
            }

            // Set format keywords
            TextureFormat format = ((Texture2D)material.mainTexture).format;
            if (format == TextureFormat.DXT1 || format == TextureFormat.DXT5)
            {
                material.DisableKeyword("_SHELL_PROTECTOR_FORMAT0");
                material.DisableKeyword("_SHELL_PROTECTOR_FORMAT1");
            }
            else if (format == TextureFormat.RGBA32)
            {
                material.DisableKeyword("_SHELL_PROTECTOR_FORMAT0");
                material.EnableKeyword("_SHELL_PROTECTOR_FORMAT1");
            }
            else if (format == TextureFormat.RGB24)
            {
                material.EnableKeyword("_SHELL_PROTECTOR_FORMAT0");
                material.DisableKeyword("_SHELL_PROTECTOR_FORMAT1");
            }
            else
            {
                Debug.LogErrorFormat("{0} - main texture is unsupported format!", material.name);
                return;
            }

            // Set rimlight keyword
            if (has_lim_texture) material.EnableKeyword("_SHELL_PROTECTOR_RIMLIGHT"); 

            // Set encryptor keyword
            material.EnableKeyword(encryptor.Keyword);
        }

        public Shader Inject(Material mat, string decode_dir, string output_dir, Texture2D tex, bool has_lim_texture = false, bool has_lim_texture2 = false, bool outline_tex = false) {
            SetKeywords(mat, has_lim_texture);
            return CustomInject(mat, decode_dir, output_dir, tex, has_lim_texture, has_lim_texture2, outline_tex);
        }

        protected abstract Shader CustomInject(Material mat, string decode_dir, string output_dir, Texture2D tex, bool has_lim_texture = false, bool has_lim_texture2 = false, bool outline_tex = false);
    }
}
#endif