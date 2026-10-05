#if UNITY_EDITOR
using System;

namespace Shell.Protector.Tests
{
    internal static class TestKeys
    {
        public const string Password = "pass";
        public const string Salt = "00112233445566778899aabbccddeeff";

        static UserKey userKey;

        // Deriving runs the full PBKDF2, so the tests share one instance.
        public static UserKey UserKey => userKey ?? (userKey = UserKey.Derive(Password, Salt, 12));

        // An arbitrary full key for texture tests: "pass" from the fixed password, then 12 user key bytes.
        public static byte[] Default => KeyGenerator.MakeKeyBytes("password", FromHex("a72e839d8da3b9806b18c877"));

        public static byte[] FromHex(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; ++i)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        public static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
#endif
