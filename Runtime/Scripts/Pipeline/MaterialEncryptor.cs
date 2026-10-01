#if UNITY_EDITOR
using UnityEngine;

namespace Shell.Protector
{
    public static class MaterialEncryptor
    {
        public static Material CreateEncryptedMaterial(Material source, Shader shader, Texture2D fallback, Texture2D mip, AuxiliaryTextures auxiliary, EncryptResult encrypted, byte[] keyBytes, int fixedKeySize, bool turnOnAllSafetyFallback, Injector injector)
        {
            Material result = new Material(source);
            result.shader = shader;
            var originalTex = (Texture2D)result.mainTexture;
            injector.SetKeywords(result, auxiliary.LimTexture != null);
            ConfigureDecryption(result, originalTex, encrypted, keyBytes, fixedKeySize, (uint)source.GetInstanceID());
            result.mainTexture = fallback;
            result.SetTexture(ShaderProperties.MipTexture, mip);
            result.renderQueue = source.renderQueue;
            if (turnOnAllSafetyFallback)
                result.SetOverrideTag("VRCFallback", "Unlit");
            return result;
        }

        // Configures an in-memory material; callers own asset persistence and shader injection.
        public static void ConfigureDecryption(Material result, Texture2D original, EncryptResult encrypted, byte[] keyBytes, int fixedKeySize, uint hashMagic)
        {
            result.SetTexture(ShaderProperties.EncryptTexture0, encrypted.Texture1);
            result.SetTexture(ShaderProperties.EncryptTexture1, encrypted.Texture2 ?? Texture2D.blackTexture);
            for (int i = 0; i < fixedKeySize; ++i)
                result.SetFloat(ShaderProperties.KeyPrefix + i, keyBytes[i]);

            var cipher = encrypted.Cipher;
            result.DisableKeyword(ShaderProperties.ChachaKeyword);
            result.DisableKeyword(ShaderProperties.XXTEAKeyword);
            result.EnableKeyword(cipher.Keyword);
            if (cipher.Keyword == ShaderProperties.ChachaKeyword)
            {
                result.SetInteger(ShaderProperties.Nonce0, unchecked((int)cipher.Nonce0));
                result.SetInteger(ShaderProperties.Nonce1, unchecked((int)cipher.Nonce1));
                result.SetInteger(ShaderProperties.Nonce2, unchecked((int)cipher.Nonce2));
            }
            else if (cipher.Keyword == ShaderProperties.XXTEAKeyword)
                result.SetInteger(ShaderProperties.Rounds, (int)cipher.Rounds);

            var hash = KeyGenerator.SimpleHash(keyBytes, hashMagic);
            result.SetInteger(ShaderProperties.HashMagic, (int)hashMagic);
            result.SetInteger(ShaderProperties.PasswordHash, (int)hash);
            TextureEncryptManager.ConfigureMaterial(result, original, encrypted);
        }
    }
}
#endif
