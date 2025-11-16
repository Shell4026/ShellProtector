using System.IO;
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

        protected string shader_code_nofilter = @"
				half4 mainTexture;

                UNITY_BRANCH
                if(IsDecrypted())
                {
				    mainTexture = DecryptTextureBox(_EncryptTex0, _EncryptTex1, sampler_EncryptTex0, _EncryptTex0_TexelSize, _MipTex, sampler_MipTex, mainUV);
                }
                else
                {
                    mainTexture = _MainTex.Sample(sampler_MainTex, mainUV);
                }
        ";
        protected string shader_code_bilinear = @"
                half4 mainTexture;

                UNITY_BRANCH
                if(IsDecrypted())
                {
				    mainTexture = DecryptTextureBilinear(_EncryptTex0, _EncryptTex1, sampler_EncryptTex0, _EncryptTex0_TexelSize, _MipTex, sampler_MipTex, mainUV);
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

        public void SetKeywords(Material material, bool has_lim_texture = false) {
            // Clear keywords prefixed with _SHELL_PROTECTOR_
            var keywords = material.shaderKeywords;
            foreach (string keyword in keywords)
            {
                if (keyword.StartsWith("_SHELL_PROTECTOR_")) {
                    material.DisableKeyword(keyword);
                }
            }

            // Set format keywords
            TextureEncryptManager.SetFormatKeywords(material);

            // Set rimlight keyword
            if (has_lim_texture) 
                material.EnableKeyword("_SHELL_PROTECTOR_RIMLIGHT"); 

            // Set encryptor keyword
            material.EnableKeyword(encryptor.Keyword);
        }

        public Shader Inject(Material mat, string decode_dir, string output_dir, Texture2D tex, bool has_lim_texture = false, bool has_lim_texture2 = false, bool outline_tex = false) 
        {
            return CustomInject(mat, decode_dir, output_dir, tex, has_lim_texture, has_lim_texture2, outline_tex);
        }

        protected abstract Shader CustomInject(Material mat, string decode_dir, string output_dir, Texture2D tex, bool has_lim_texture = false, bool has_lim_texture2 = false, bool outline_tex = false);
    }
}
#endif