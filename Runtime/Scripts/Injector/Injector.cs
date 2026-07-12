using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace Shell.Protector
{
    public abstract class Injector : IShaderAdapter
    {
        protected readonly ushort[] Keys = new ushort[8]; // 16 byte
        protected readonly AssetManager ShaderManager = AssetManager.GetInstance();
        protected int Filter = 1;
        protected string AssetDir;
        protected int UserKeyLength = 4;

        protected string ShaderCodeNoFilter = @"
				half4 mainTexture;

                UNITY_BRANCH
                if(isDecrypted)
                {
				    mainTexture = DecryptTextureBox(_EncryptTex0, _EncryptTex1, sampler_EncryptTex0, _EncryptTex0_TexelSize, _MipTex, sampler_MipTex, mainUV);
                }
                else
                {
                    mainTexture = _MainTex.Sample(sampler_MainTex, mainUV);
                }
        ";
        protected string ShaderCodeBilinear = @"
                half4 mainTexture;

                UNITY_BRANCH
                if(isDecrypted)
                {
				    mainTexture = DecryptTextureBilinear(_EncryptTex0, _EncryptTex1, sampler_EncryptTex0, _EncryptTex0_TexelSize, _MipTex, sampler_MipTex, mainUV);
                }
                else
                {
                    mainTexture = _MainTex.Sample(sampler_MainTex, mainUV);
                }
        ";

        protected GameObject Target;
        protected Texture2D MainTexture;
        protected IEncryptor Encryptor;

        protected struct Decoder
        {
            public Decoder(string decrypt = null, string xxtea = null, string chacha = null)
            {
                Decrypt = decrypt;
                Xxtea = xxtea;
                Chacha = chacha;
            }
            public string Decrypt;
            public string Xxtea;
            public string Chacha;
        }

        public void Init(GameObject target, Texture2D mainTexture, byte[] key, int userKeyLength, int filter, string assetDir, IEncryptor encryptor)
        {
            if (key.Length != 16)
            {
                Debug.LogError("Key bytes requires 16 byte");
                return;
            }
            Target = target;
            for (int i = 0, j = 0; i < Keys.Length; ++i, j += 2)
            {
                Keys[i] = (ushort)(key[j] | key[j + 1] << 8);
            }
            MainTexture = mainTexture;
            Filter = filter;
            AssetDir = assetDir;
            UserKeyLength = userKeyLength;
            Encryptor = encryptor;
        }

        public abstract bool CanHandle(Shader shader);

        public bool WasInjected(Shader shader)
        {
            string shader_path = AssetDatabase.GetAssetPath(shader);

            string shader_data = File.ReadAllText(shader_path);
            if (shader_data.Contains("//ShellProtect"))
                return true;
            return false;
        }

        public void SetKeywords(Material material, bool hasLimTexture = false)
        {
            // Clear keywords prefixed with _SHELL_PROTECTOR_
            var keywords = material.shaderKeywords;
            foreach (string keyword in keywords)
            {
                if (keyword.StartsWith(ShaderProperties.KeywordPrefix)) {
                    material.DisableKeyword(keyword);
                }
            }

            // Set format keywords
            TextureEncryptManager.SetFormatKeywords(material);

            // Set rimlight keyword
            if (hasLimTexture)
                material.EnableKeyword(ShaderProperties.RimLightKeyword);

            // Set encryptor keyword
            material.EnableKeyword(Encryptor.Keyword);
        }

        public Shader Inject(Material material, string decoderPath, string outputPath, Texture2D mainTexture, AuxiliaryTextures auxiliaryTextures)
        {
            return Inject(
                material,
                decoderPath,
                outputPath,
                mainTexture,
                auxiliaryTextures.LimTexture != null,
                auxiliaryTextures.LimTexture2 != null,
                auxiliaryTextures.OutlineTexture != null
            );
        }

        public Shader Inject(Material mat, string decodeDir, string outputDir, Texture2D tex, bool hasLimTexture = false, bool hasLimTexture2 = false, bool outlineTex = false)
        {
            return CustomInject(mat, decodeDir, outputDir, tex, hasLimTexture, hasLimTexture2, outlineTex);
        }

        protected abstract Shader CustomInject(Material mat, string decodeDir, string outputDir, Texture2D tex, bool hasLimTexture = false, bool hasLimTexture2 = false, bool outlineTex = false);
    }
}
#endif
