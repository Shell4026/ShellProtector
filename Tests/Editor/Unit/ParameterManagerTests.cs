#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shell.Protector.Tests.Unit
{
    public class ParameterManagerTests
    {
        [Test]
        public void AddKeyParameter_UsesLegacyNamesForSyncSizeOne()
        {
            ScriptableObject original = CreateBaseParameters();

            ScriptableObject result = VrcExpressionParametersTestUtil.AddKeyParameter(original, 12, 1);
            VrcExpressionParametersTestUtil.ParameterSnapshot[] parameters = VrcExpressionParametersTestUtil.Read(result).ToArray();

            Assert.That(result.name, Is.EqualTo("BaseParams_encrypted"));
            Assert.That(parameters.Length, Is.EqualTo(19));
            AssertParameter(parameters, "existing", false, false, "Bool");
            AssertParameter(parameters, "encrypt_lock", true, true, "Bool");
            AssertParameter(parameters, "pkey", true, true, "Float");
            AssertParameter(parameters, "encrypt_switch0", true, true, "Bool");
            AssertParameter(parameters, "encrypt_switch3", true, true, "Bool");
            AssertParameter(parameters, "SHELL_PROTECTOR_key11", false, false, "Float");
            Assert.That(parameters.Any(p => p.Name == "SHELL_PROTECTOR_saved_key0"), Is.False);
        }

        [Test]
        public void AddKeyParameter_UsesPrefixedNamesForMultiSync()
        {
            ScriptableObject original = CreateBaseParameters();

            ScriptableObject result = VrcExpressionParametersTestUtil.AddKeyParameter(original, 12, 3);
            VrcExpressionParametersTestUtil.ParameterSnapshot[] parameters = VrcExpressionParametersTestUtil.Read(result).ToArray();

            Assert.That(parameters.Length, Is.EqualTo(31));
            AssertParameter(parameters, "SHELL_PROTECTOR_sync_lock", true, true, "Bool");
            AssertParameter(parameters, "SHELL_PROTECTOR_synced_key0", true, true, "Float");
            AssertParameter(parameters, "SHELL_PROTECTOR_synced_key2", true, true, "Float");
            AssertParameter(parameters, "SHELL_PROTECTOR_sync_switch1", true, true, "Bool");
            AssertParameter(parameters, "SHELL_PROTECTOR_key11", false, false, "Float");
            AssertParameter(parameters, "SHELL_PROTECTOR_saved_key11", true, false, "Float");
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
