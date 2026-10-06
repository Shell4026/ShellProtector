#if UNITY_EDITOR
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Shell.Protector
{
    // Values compiled into a generated shader instead of stored in its materials: a mask XORed into the four key words
    // and the four ChaCha constant words. The material and the captured key parameters are then not enough to decrypt
    // a texture offline; the values have to be dug out of each shader's bytecode.
    // Protector.cginc reads them from the macros that ToDefines writes. A shader without them uses None.
    // Each Poiyomi shader copy bakes the secrets of its main texture (EncryptedHistory), and the lilToon shaders bake the
    // project's (LilToonShaders).
    [Serializable]
    public sealed class ShaderSecrets
    {
        public const int WordCount = 4;

        [SerializeField] uint[] keyMask;
        [SerializeField] uint[] chachaConstants;

        // For Unity's serializer and JsonUtility.
        ShaderSecrets()
        {
        }

        public ShaderSecrets(uint[] keyMask, uint[] chachaConstants)
        {
            if (keyMask == null || keyMask.Length != WordCount)
                throw new ArgumentException("The key mask must have " + WordCount + " words.", nameof(keyMask));
            if (chachaConstants == null || chachaConstants.Length != WordCount)
                throw new ArgumentException("The ChaCha constants must have " + WordCount + " words.", nameof(chachaConstants));

            this.keyMask = (uint[])keyMask.Clone();
            this.chachaConstants = (uint[])chachaConstants.Clone();
        }

        public static ShaderSecrets None => new ShaderSecrets(new uint[WordCount], Chacha20.StandardConstants);

        // False for a field that Unity deserialized from data saved before it existed.
        public bool IsValid => keyMask != null && keyMask.Length == WordCount && chachaConstants != null && chachaConstants.Length == WordCount;

        public uint[] KeyMask => (uint[])keyMask.Clone();
        public uint[] ChachaConstants => (uint[])chachaConstants.Clone();

        public static ShaderSecrets Generate()
        {
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                uint[] mask = RandomWords(random);
                uint[] constants;
                // Distinct constants keep the four columns of the ChaCha state from starting out equal.
                do
                    constants = RandomWords(random);
                while (HasDuplicate(constants));
                return new ShaderSecrets(mask, constants);
            }
        }

        // Key words are little-endian, so masking the key bytes equals XORing the mask into the key words in the shader.
        public byte[] MaskKey(byte[] key)
        {
            byte[] masked = (byte[])key.Clone();
            for (int i = 0; i < masked.Length && i < WordCount * 4; ++i)
                masked[i] ^= (byte)(keyMask[i / 4] >> (i % 4 * 8));
            return masked;
        }

        public bool Matches(ShaderSecrets other)
        {
            if (other == null || !IsValid || !other.IsValid)
                return false;
            for (int i = 0; i < WordCount; ++i)
            {
                if (keyMask[i] != other.keyMask[i] || chachaConstants[i] != other.chachaConstants[i])
                    return false;
            }
            return true;
        }

        // Guarded so a shader that includes it once per pass, or next to another copy, still compiles.
        public string ToDefines()
        {
            var builder = new StringBuilder();
            builder.Append("#ifndef _SHELL_PROTECTOR_SECRETS\n");
            builder.Append("#define _SHELL_PROTECTOR_SECRETS\n");
            for (int i = 0; i < WordCount; ++i)
                builder.AppendFormat("#define _SHELL_PROTECTOR_KEY_MASK{0} 0x{1:x8}u\n", i, keyMask[i]);
            for (int i = 0; i < WordCount; ++i)
                builder.AppendFormat("#define _SHELL_PROTECTOR_CHACHA_C{0} 0x{1:x8}u\n", i, chachaConstants[i]);
            builder.Append("#endif\n");
            return builder.ToString();
        }

        static uint[] RandomWords(RandomNumberGenerator random)
        {
            byte[] bytes = new byte[WordCount * 4];
            random.GetBytes(bytes);
            uint[] words = new uint[WordCount];
            for (int i = 0; i < WordCount; ++i)
                words[i] = BitConverter.ToUInt32(bytes, i * 4);
            return words;
        }

        static bool HasDuplicate(uint[] words)
        {
            for (int i = 0; i < words.Length; ++i)
            {
                for (int j = i + 1; j < words.Length; ++j)
                {
                    if (words[i] == words[j])
                        return true;
                }
            }
            return false;
        }
    }
}
#endif
