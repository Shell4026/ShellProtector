#if UNITY_EDITOR
using UnityEngine;

namespace Shell.Protector
{
    internal sealed class MaterialEncryptor
    {
        readonly AssetWriter writer;
        readonly bool turnOnAllSafetyFallback;
        readonly int algorithm;
        readonly uint rounds;

        public MaterialEncryptor(AssetWriter writer, bool turnOnAllSafetyFallback, int algorithm, uint rounds)
        {
            this.writer = writer;
            this.turnOnAllSafetyFallback = turnOnAllSafetyFallback;
            this.algorithm = algorithm;
            this.rounds = rounds;
        }

        public Material CreateEncryptedMaterial(string folderGuid, string fileName, Material source, Shader shader, Texture2D fallback, Texture2D mip, AuxiliaryTextures auxiliary, ProcessedTexture texture, byte[] keyBytes, int fixedKeySize, IEncryptor encryptor, Injector injector)
        {
            Material result = new Material(source.shader);
            result.CopyPropertiesFromMaterial(source);
            result.shader = shader;
            var originalTex = (Texture2D)result.mainTexture;
            result.mainTexture = fallback;

            if (texture.Encrypted.Texture1 != null)
                result.SetTexture(ShaderProperties.EncryptTexture0, texture.Encrypted.Texture1);
            if (texture.Encrypted.Texture2 != null)
                result.SetTexture(ShaderProperties.EncryptTexture1, texture.Encrypted.Texture2);

            result.SetTexture(ShaderProperties.MipTexture, mip);
            result.renderQueue = source.renderQueue;
            if (turnOnAllSafetyFallback)
                result.SetOverrideTag("VRCFallback", "Unlit");

            var (widthOffset, heightOffset) = TextureEncryptManager.CalculateOffsets(originalTex);
            result.SetInteger(ShaderProperties.WidthOffset, widthOffset);
            result.SetInteger(ShaderProperties.HeightOffset, heightOffset);
            for (int i = 0; i < fixedKeySize; ++i)
                result.SetFloat(ShaderProperties.KeyPrefix + i, keyBytes[i]);

            if (algorithm == (int)ShellProtectorAlgorithm.Chacha)
            {
                Chacha20 chacha = encryptor as Chacha20;
                result.SetInteger(ShaderProperties.Nonce0, (int)chacha.GetNonceUint3()[0]);
                result.SetInteger(ShaderProperties.Nonce1, (int)chacha.GetNonceUint3()[1]);
                result.SetInteger(ShaderProperties.Nonce2, (int)chacha.GetNonceUint3()[2]);
            }
            else if (algorithm == (int)ShellProtectorAlgorithm.XXTEA)
            {
                result.SetInteger(ShaderProperties.Rounds, (int)rounds);
            }

            var key = new byte[16];
            for (int i = 0; i < 16; i++)
                key[i] = keyBytes[i];

            uint hashMagic = (uint)source.GetInstanceID();
            var hash = KeyGenerator.SimpleHash(key, hashMagic);
            result.SetInteger(ShaderProperties.HashMagic, (int)hashMagic);
            result.SetInteger(ShaderProperties.PasswordHash, (int)hash);

            injector.SetKeywords(result, auxiliary.LimTexture != null);
            TextureEncryptManager.SetFormatKeywords(result, originalTex);
            writer.CreateAssetInFolder(result, folderGuid, fileName);
            writer.SaveAndRefresh();
            return result;
        }
    }
}
#endif
