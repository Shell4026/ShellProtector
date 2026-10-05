#if UNITY_EDITOR
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Shell.Protector.Tests.Unit
{
    // The expected values come from Python's hashlib/hmac. The OSC app checks the same values,
    // so a change here breaks the C# <-> OSC contract.
    public class UserKeyTests
    {
        [Test]
        public void Pbkdf2Sha256_MatchesReferenceVectors()
        {
            byte[] single = UserKey.Pbkdf2Sha256(Encoding.ASCII.GetBytes("passwd"), Encoding.ASCII.GetBytes("salt"), 1);
            Assert.That(TestKeys.ToHex(single), Is.EqualTo("55ac046e56e3089fec1691c22544b605f94185216dde0465e68b9d57c20dacbc"));

            // RFC 7914 section 11, first 32 bytes
            byte[] many = UserKey.Pbkdf2Sha256(Encoding.ASCII.GetBytes("Password"), Encoding.ASCII.GetBytes("NaCl"), 80000);
            Assert.That(TestKeys.ToHex(many), Is.EqualTo("4ddcd8f60b98be21830cee5ef22701f9641a4418d04c0414aeff08876b34ab56"));
        }

        [Test]
        public void Derive_MatchesKnownKeyBytes()
        {
            Assert.That(TestKeys.ToHex(TestKeys.UserKey.GetKeyBytes()), Is.EqualTo("692630f20e7d0757589dc385"));
        }

        [TestCase("encrypt_lock", "5910e3d25e275e38")]
        [TestCase("pkey", "d451302904d9bce0")]
        [TestCase("pkey0", "bd8b03335879e1fa")]
        [TestCase("encrypt_switch0", "b3427dc109865271")]
        [TestCase("encrypt_switch3", "8bd9e5daac8f06df")]
        [TestCase("key0", "04f2c9c6adb2b921")]
        [TestCase("key11", "fb451c0ab9fb2616")]
        [TestCase("saved_key0", "f9cf1cecb61a394f")]
        public void ObfuscateParameter_MatchesKnownNames(string name, string expected)
        {
            Assert.That(TestKeys.UserKey.ObfuscateParameter(name), Is.EqualTo(expected));
        }

        [Test]
        public void SaltParameterName_ExposesTheSalt()
        {
            Assert.That(TestKeys.UserKey.SaltParameterName, Is.EqualTo("SP_SALT_" + TestKeys.Salt));
        }

        [Test]
        public void GetPasswordBytes_TruncatesToKeyLength()
        {
            Assert.That(UserKey.GetPasswordBytes("passwordlong", 4), Is.EqualTo(Encoding.ASCII.GetBytes("pass")));
            Assert.That(UserKey.GetPasswordBytes("pass", 12), Is.EqualTo(Encoding.ASCII.GetBytes("pass")));
            Assert.That(UserKey.GetPasswordBytes(null, 4), Is.Empty);
        }

        [Test]
        public void GenerateSalt_ReturnsValidUniqueSalts()
        {
            string a = UserKey.GenerateSalt();
            string b = UserKey.GenerateSalt();

            Assert.That(UserKey.IsValidSalt(a), Is.True);
            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(UserKey.IsValidSalt("00112233445566778899AABBCCDDEEFF"), Is.False);
            Assert.That(UserKey.IsValidSalt(""), Is.False);
        }

        [Test]
        public void Derive_RejectsInvalidSalt()
        {
            Assert.That(() => UserKey.Derive("pass", "salt", 12), Throws.ArgumentException);
        }

        [Test]
        public void ObfuscatedNames_AreDistinct()
        {
            string[] names = { "encrypt_lock", "pkey", "encrypt_switch0", "encrypt_switch1", "key0", "key1", "saved_key0" };
            Assert.That(names.Select(TestKeys.UserKey.ObfuscateParameter).Distinct().Count(), Is.EqualTo(names.Length));
        }
    }
}
#endif
