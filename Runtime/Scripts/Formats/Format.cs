using UnityEngine;

namespace Shell.Protector
{
    public struct EncryptResult {
        public Texture2D Texture1;
        public Texture2D Texture2;
        public BC7TextureLayout Layout;
        public readonly EncryptionParameters Cipher;

        public EncryptResult(IEncryptor algorithm) {
            Texture1 = Texture2 = null;
            Layout = null;
            Cipher = new EncryptionParameters(algorithm);
        }
    }

    // Value snapshot: later encryptions cannot change how this result is decrypted.
    public readonly struct EncryptionParameters {
        public readonly string Keyword;
        public readonly uint Nonce0, Nonce1, Nonce2, Rounds;

        public EncryptionParameters(IEncryptor algorithm) {
            Keyword = algorithm.Keyword;
            Nonce0 = Nonce1 = Nonce2 = Rounds = 0;
            if (algorithm is Chacha20 chacha) {
                uint[] nonce = chacha.GetNonceUint3();
                Nonce0 = nonce[0]; Nonce1 = nonce[1]; Nonce2 = nonce[2];
            }
            else if (algorithm is XXTEA xxtea)
                Rounds = xxtea.Rounds;
        }
    }

    public interface ITextureFormat {
        bool CanHandle(TextureFormat format);
        EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm);
        void SetFormatKeywords(Material material);
        (int, int) CalculateOffsets(Texture2D texture);
    }

    public abstract class BaseTextureFormat : ITextureFormat {
        protected uint[] ConvertKeyToUInt(byte[] key) {
            uint[] key_uint = new uint[4];
            key_uint[0] = (uint)(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
            key_uint[1] = (uint)(key[4] | (key[5] << 8) | (key[6] << 16) | (key[7] << 24));
            key_uint[2] = (uint)(key[8] | (key[9] << 8) | (key[10] << 16) | (key[11] << 24));
            key_uint[3] = 0;
            return key_uint;
        }

        // Last key word for one encryption unit. The mip level goes in the top 8 bits (unit indices stay below 2^24),
        // otherwise the same index on every mip level would reuse one ChaCha keystream.
        protected static uint GetUnitKey(byte[] key, uint idx, int mip) {
            return (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24)) ^ idx ^ ((uint)mip << 24);
        }

        internal virtual void Validate(Texture2D texture, int mipCount, ShellProtectorAlgorithm algorithm) { }

        // Formats whose shader decoder only implements ChaCha8 ignore the XXTEA setting.
        internal virtual bool RequiresChacha => false;

        internal virtual (int width, int height, bool fullChain) MipReference(Texture2D texture) {
            int size = Mathf.Max(texture.width, texture.height);
            return (size, size, false);
        }

        internal virtual int FallbackSize(Texture2D texture, int requestedSize) => requestedSize;

        internal virtual void ConfigureMaterial(Material material, Texture2D original, EncryptResult encrypted) {
            SetFormatKeywords(material);
            var (width, height) = CalculateOffsets(original);
            material.SetInteger(ShaderProperties.WidthOffset, width);
            material.SetInteger(ShaderProperties.HeightOffset, height);
        }

        public abstract bool CanHandle(TextureFormat format);
        public abstract EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm);
        public abstract void SetFormatKeywords(Material material);
        public abstract (int, int) CalculateOffsets(Texture2D texture);
    }
}
