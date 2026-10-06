#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shell.Protector.Tests.Unit
{
    public class ShaderSecretsTests
    {
        [Test]
        public void None_LeavesKeyAndChachaConstantsUnchanged()
        {
            ShaderSecrets none = ShaderSecrets.None;

            Assert.That(none.MaskKey(TestKeys.Default), Is.EqualTo(TestKeys.Default));
            Assert.That(none.ChachaConstants, Is.EqualTo(Chacha20.StandardConstants));
        }

        [Test]
        public void MaskKey_XorsTheMaskAsLittleEndianWords()
        {
            byte[] masked = TestKeys.Secrets.MaskKey(new byte[16]);

            Assert.That(TestKeys.ToHex(masked), Is.EqualTo("b979379e157c4a7fbb49d09491f44525"));
        }

        [Test]
        public void ToDefines_WritesGuardedMacros()
        {
            Assert.That(TestKeys.Secrets.ToDefines(), Is.EqualTo(
                "#ifndef _SHELL_PROTECTOR_SECRETS\n" +
                "#define _SHELL_PROTECTOR_SECRETS\n" +
                "#define _SHELL_PROTECTOR_KEY_MASK0 0x9e3779b9u\n" +
                "#define _SHELL_PROTECTOR_KEY_MASK1 0x7f4a7c15u\n" +
                "#define _SHELL_PROTECTOR_KEY_MASK2 0x94d049bbu\n" +
                "#define _SHELL_PROTECTOR_KEY_MASK3 0x2545f491u\n" +
                "#define _SHELL_PROTECTOR_CHACHA_C0 0x1b873593u\n" +
                "#define _SHELL_PROTECTOR_CHACHA_C1 0xcc9e2d51u\n" +
                "#define _SHELL_PROTECTOR_CHACHA_C2 0x85ebca6bu\n" +
                "#define _SHELL_PROTECTOR_CHACHA_C3 0xc2b2ae35u\n" +
                "#endif\n"));
        }

        [Test]
        public void Generate_ReturnsDifferentValidSecrets()
        {
            ShaderSecrets first = ShaderSecrets.Generate();
            ShaderSecrets second = ShaderSecrets.Generate();

            Assert.That(first.IsValid, Is.True);
            Assert.That(first.ChachaConstants.Distinct().Count(), Is.EqualTo(ShaderSecrets.WordCount));
            Assert.That(first.Matches(second), Is.False);
        }

        [Test]
        public void Matches_ComparesValues()
        {
            Assert.That(TestKeys.Secrets.Matches(TestKeys.Secrets), Is.True);
            Assert.That(TestKeys.Secrets.Matches(ShaderSecrets.None), Is.False);
            Assert.That(TestKeys.Secrets.Matches(null), Is.False);
        }

        [Test]
        public void JsonRoundTrip_KeepsValues()
        {
            ShaderSecrets loaded = JsonUtility.FromJson<ShaderSecrets>(JsonUtility.ToJson(TestKeys.Secrets));

            Assert.That(loaded.Matches(TestKeys.Secrets), Is.True);
        }

        [Test]
        public void Chacha20_WithOtherConstants_ChangesKeystreamAndRoundTrips()
        {
            uint[] key = { 1, 2, 3, 4 };
            uint[] data = Enumerable.Range(0, 32).Select(i => (uint)i).ToArray();
            var standard = new Chacha20();
            var custom = new Chacha20 { Constants = TestKeys.Secrets.ChachaConstants };

            uint[] encrypted = custom.Encrypt(data, key);

            Assert.That(encrypted, Is.Not.EqualTo(standard.Encrypt(data, key)));
            Assert.That(custom.Decrypt(encrypted, key), Is.EqualTo(data));
        }
    }
}
#endif
