#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shell.Protector.Tests.Unit
{
    public class ParameterManagerTests
    {
        [Test]
        public void AddKeyParameter_ObfuscatesLegacyNamesForSyncSizeOne()
        {
            ScriptableObject original = CreateBaseParameters();

            ScriptableObject result = VrcExpressionParametersTestUtil.AddKeyParameter(original, 12, 1, TestKeys.UserKey);
            VrcExpressionParametersTestUtil.ParameterSnapshot[] parameters = VrcExpressionParametersTestUtil.Read(result).ToArray();

            Assert.That(result.name, Is.EqualTo("BaseParams_encrypted"));
            Assert.That(parameters.Length, Is.EqualTo(20));
            AssertParameter(parameters, "existing", false, false, "Bool");
            AssertParameter(parameters, TestKeys.UserKey.SaltParameterName, false, false, "Bool");
            AssertParameter(parameters, Obfuscated("encrypt_lock"), true, true, "Bool");
            AssertParameter(parameters, Obfuscated("pkey"), true, true, "Float");
            AssertParameter(parameters, Obfuscated("encrypt_switch0"), true, true, "Bool");
            AssertParameter(parameters, Obfuscated("encrypt_switch3"), true, true, "Bool");
            AssertParameter(parameters, Obfuscated("key11"), false, false, "Float");
            Assert.That(parameters.Any(p => p.Name == Obfuscated("saved_key0")), Is.False);
            AssertNoReadableNames(parameters);
        }

        [Test]
        public void AddKeyParameter_ObfuscatesNamesForMultiSync()
        {
            ScriptableObject original = CreateBaseParameters();

            ScriptableObject result = VrcExpressionParametersTestUtil.AddKeyParameter(original, 12, 4, TestKeys.UserKey);
            VrcExpressionParametersTestUtil.ParameterSnapshot[] parameters = VrcExpressionParametersTestUtil.Read(result).ToArray();

            // existing + salt + lock + 4 synced keys + 2 switches (3 steps) + 12 keys + 12 saved keys
            Assert.That(parameters.Length, Is.EqualTo(33));
            AssertParameter(parameters, TestKeys.UserKey.SaltParameterName, false, false, "Bool");
            AssertParameter(parameters, Obfuscated("sync_lock"), true, true, "Bool");
            AssertParameter(parameters, Obfuscated("pkey0"), true, true, "Float");
            AssertParameter(parameters, Obfuscated("pkey3"), true, true, "Float");
            AssertParameter(parameters, Obfuscated("sync_switch1"), true, true, "Bool");
            AssertParameter(parameters, Obfuscated("key11"), false, false, "Float");
            AssertParameter(parameters, Obfuscated("saved_key11"), true, false, "Float");
            AssertNoReadableNames(parameters);
        }

        // The OSC app sends the sync speed 1 protocol to every avatar, so the mux layer's lock and switches must not
        // share names with it.
        [Test]
        public void AddKeyParameter_MultiSyncDoesNotShareOscMultiplexingNames()
        {
            ScriptableObject result = VrcExpressionParametersTestUtil.AddKeyParameter(CreateBaseParameters(), 12, 2, TestKeys.UserKey);
            string[] names = VrcExpressionParametersTestUtil.Read(result).Select(p => p.Name).ToArray();

            string[] oscNames = { "encrypt_lock", "encrypt_switch0", "encrypt_switch1", "encrypt_switch2", "encrypt_switch3", "pkey" };
            Assert.That(names.Intersect(oscNames.Select(Obfuscated)), Is.Empty);
        }

        private static string Obfuscated(string name) => TestKeys.UserKey.ObfuscateParameter(name);

        private static void AssertNoReadableNames(VrcExpressionParametersTestUtil.ParameterSnapshot[] parameters)
        {
            string[] readable = { "pkey", "encrypt_lock", "encrypt_switch", "SHELL_PROTECTOR_" };
            Assert.That(parameters.Where(p => readable.Any(r => p.Name.StartsWith(r))).Select(p => p.Name), Is.Empty);
        }

        private static ScriptableObject CreateBaseParameters()
        {
            return VrcExpressionParametersTestUtil.Create(
                "BaseParams",
                new VrcExpressionParametersTestUtil.ParameterSpec
                {
                    Name = "existing",
                    Saved = false,
                    NetworkSynced = false,
                    ValueType = "Bool",
                    DefaultValue = 0f
                });
        }

        private static void AssertParameter(VrcExpressionParametersTestUtil.ParameterSnapshot[] parameters, string name, bool saved, bool networkSynced, string valueType)
        {
            VrcExpressionParametersTestUtil.ParameterSnapshot parameter = parameters.Single(p => p.Name == name);
            Assert.That(parameter.Saved, Is.EqualTo(saved), name);
            Assert.That(parameter.NetworkSynced, Is.EqualTo(networkSynced), name);
            Assert.That(parameter.ValueType, Is.EqualTo(valueType), name);
            Assert.That(parameter.DefaultValue, Is.EqualTo(0f), name);
        }
    }
}
#endif
