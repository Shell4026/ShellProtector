#if UNITY_EDITOR
using System;
using System.Security.Cryptography;
using System.Text;

namespace Shell.Protector
{
    // The user part of the key and the avatar parameter names, both derived from the user password
    // with a per-avatar salt. The OSC app derives the same values, so this must match it exactly:
    //   password = UTF-8 bytes of the user password, truncated to the key length
    //   salt     = ASCII bytes of the 32-character lowercase hex salt string
    //   derived  = PBKDF2-HMAC-SHA256(password, salt, Iterations, 32 bytes)
    //   key      = derived[0, keyLength)
    //   name(n)  = lowercase hex of HMAC-SHA256(derived[16, 32), UTF-8 n), first 8 bytes
    // The salt is published to the OSC app through a local-only parameter named SaltParameterPrefix + salt,
    // which VRChat lists in the avatar's OSC config.
    public sealed class UserKey
    {
        public const int Iterations = 600000;
        public const int MaxLength = 16;
        public const int SaltLength = 16;
        public const string SaltParameterPrefix = "SP_SALT_";

        readonly byte[] derived;

        public string Salt { get; }
        public int Length { get; }
        public string SaltParameterName => SaltParameterPrefix + Salt;

        UserKey(byte[] derived, string salt, int length)
        {
            this.derived = derived;
            Salt = salt;
            Length = length;
        }

        public static UserKey Derive(string password, string salt, int length)
        {
            if (!IsValidSalt(salt))
                throw new ArgumentException("The salt must be " + SaltLength * 2 + " lowercase hex characters.", nameof(salt));
            if (length < 0 || length > MaxLength)
                throw new ArgumentOutOfRangeException(nameof(length));

            byte[] derived = Pbkdf2Sha256(GetPasswordBytes(password, length), Encoding.ASCII.GetBytes(salt), Iterations);
            return new UserKey(derived, salt, length);
        }

        public static string GenerateSalt()
        {
            byte[] bytes = new byte[SaltLength];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return ToHex(bytes, bytes.Length);
        }

        public static bool IsValidSalt(string salt)
        {
            if (salt == null || salt.Length != SaltLength * 2)
                return false;
            foreach (char c in salt)
            {
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f'))
                    return false;
            }
            return true;
        }

        // Only the first `length` bytes of the password are part of the key.
        public static byte[] GetPasswordBytes(string password, int length)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(password ?? "");
            if (bytes.Length <= length)
                return bytes;
            byte[] truncated = new byte[length];
            Array.Copy(bytes, truncated, length);
            return truncated;
        }

        public byte[] GetKeyBytes()
        {
            byte[] key = new byte[Length];
            Array.Copy(derived, key, Length);
            return key;
        }

        public string ObfuscateParameter(string name)
        {
            byte[] nameKey = new byte[16];
            Array.Copy(derived, 16, nameKey, 0, nameKey.Length);
            using (var hmac = new HMACSHA256(nameKey))
                return ToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes(name)), 8);
        }

        // PBKDF2 with a single 32-byte output block.
        public static byte[] Pbkdf2Sha256(byte[] password, byte[] salt, int iterations)
        {
            using (var hmac = new HMACSHA256(password))
            {
                byte[] block = new byte[salt.Length + 4];
                Array.Copy(salt, block, salt.Length);
                block[block.Length - 1] = 1; // Big-endian block index 1

                byte[] u = hmac.ComputeHash(block);
                byte[] result = (byte[])u.Clone();
                for (int i = 1; i < iterations; ++i)
                {
                    u = hmac.ComputeHash(u);
                    for (int j = 0; j < result.Length; ++j)
                        result[j] ^= u[j];
                }
                return result;
            }
        }

        static string ToHex(byte[] bytes, int count)
        {
            var sb = new StringBuilder(count * 2);
            for (int i = 0; i < count; ++i)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
#endif
