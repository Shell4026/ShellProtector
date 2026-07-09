#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;

namespace Shell.Protector.Tests.Unit
{
    public class CryptoTests
    {
        [Test]
        public void MakeKeyBytes_ReturnsStablePasswordKey()
        {
            byte[] key = KeyGenerator.MakeKeyBytes("password", "pass", 12);

            Assert.That(ToHex(key), Is.EqualTo("70617373a72e839d8da3b9806b18c877"));
        }

        [Test]
        public void MakeKeyBytes_UsesFixedPasswordPrefix()
        {
            byte[] key = KeyGenerator.MakeKeyBytes("fixed", "user", 12);

            Assert.That(key, Has.Length.EqualTo(16));
            Assert.That(key[0], Is.EqualTo((byte)'f'));
            Assert.That(key[1], Is.EqualTo((byte)'i'));
        }

        [Test]
        public void SimpleHash_ReturnsStableHash()
        {
            byte[] key = KeyGenerator.MakeKeyBytes("password", "pass", 12);

            Assert.That(KeyGenerator.SimpleHash(key, 0x12345678u), Is.EqualTo(0x94f301a9u));
        }

        [Test]
        public void GenerateRandomString_UsesRequestedLengthAndAllowedCharacters()
        {
            const string allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_-+=|\\/?.>,<~`\'\" ";

            string value = KeyGenerator.GenerateRandomString(64);

            Assert.That(value, Has.Length.EqualTo(64));
            Assert.That(value.All(c => allowedChars.IndexOf(c) >= 0), Is.True);
            Assert.That(KeyGenerator.GenerateRandomString(0), Is.Empty);
        }

        [Test]
        public void Xxtea_EncryptsStableVector_AndRoundTrips()
        {
            XXTEA xxtea = new XXTEA();
            uint[] data = { 0x01234567u, 0x89abcdefu, 0xfedcba98u };
            uint[] key = { 0x00112233u, 0x44556677u, 0x8899aabbu, 0xccddeeffu };

            uint[] encrypted = xxtea.Encrypt(data, key);

            Assert.That(encrypted, Is.EqualTo(new[] { 0x6d601d05u, 0x17b6001eu, 0x9547b398u }));
            Assert.That(xxtea.Decrypt(encrypted, key), Is.EqualTo(data));
        }

        [Test]
        public void Xxtea_WithExplicitShellProtectorRounds_RoundTrips()
        {
            XXTEA xxtea = new XXTEA { Rounds = 20 };
            uint[] data = { 0x10203040u, 0x50607080u };
            uint[] key = { 0x00112233u, 0x44556677u, 0x8899aabbu, 0xccddeeffu };

            uint[] encrypted = xxtea.Encrypt(data, key);

            Assert.That(encrypted, Is.Not.EqualTo(data));
            Assert.That(xxtea.Decrypt(encrypted, key), Is.EqualTo(data));
        }

        [Test]
        public void Chacha20_EncryptsStableVector_AndRoundTrips()
        {
            Chacha20 chacha = new Chacha20();
            for (int i = 0; i < chacha.Nonce.Length; i++)
                chacha.Nonce[i] = (byte)i;

            uint[] data = { 0x00010203u, 0x04050607u, 0x08090a0bu, 0x0c0d0e0fu };
            uint[] key = { 0x00112233u, 0x44556677u, 0x8899aabbu, 0xccddeeffu };

            uint[] encrypted = chacha.Encrypt(data, key);

            Assert.That(encrypted, Is.EqualTo(new[] { 0xed4198d8u, 0x7ca6601fu, 0xde134dedu, 0x76b88629u }));
            Assert.That(chacha.Decrypt(encrypted, key), Is.EqualTo(data));
        }

        private static string ToHex(byte[] bytes)
        {
            return string.Concat(bytes.Select(b => b.ToString("x2")));
        }
    }
}
#endif
