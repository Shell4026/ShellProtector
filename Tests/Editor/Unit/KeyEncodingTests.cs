#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Shell.Protector.Tests.Unit
{
    // A key byte travels OSC app -> synced float parameter -> 1D key blend tree -> _Key material property -> round() in the shader.
    // Remote players receive the parameter quantized to a multiple of 1/127, so the byte has to survive that too.
    public class KeyEncodingTests
    {
        // Mirrors EncodeKeyByte in the OSC app (Core.cpp).
        static float Encode(int keyByte) => (keyByte - 127) / 127.0f;

        // VRChat sends synced floats as one of 255 values: -1, -126/127, ..., 1.
        static float QuantizeForNetwork(float value) => Mathf.Round(value * 127.0f) / 127.0f;

        // Simple1D blend tree with the key clips at thresholds -1 and 1, both played past their end.
        static int Decode(float parameter)
        {
            float low = AnimatorManager.CreateKeyCurve(false).Evaluate(AnimatorManager.KeyCurveEnd);
            float high = AnimatorManager.CreateKeyCurve(true).Evaluate(AnimatorManager.KeyCurveEnd);
            float weight = (parameter + 1.0f) / 2.0f;
            return Mathf.RoundToInt(low * (1.0f - weight) + high * weight);
        }

        [Test]
        public void EveryKeyByte_SurvivesLocalParameter()
        {
            for (int b = 0; b <= UserKey.MaxKeyByte; ++b)
                Assert.That(Decode(Encode(b)), Is.EqualTo(b), "key byte " + b);
        }

        [Test]
        public void EveryKeyByte_SurvivesNetworkQuantization()
        {
            for (int b = 0; b <= UserKey.MaxKeyByte; ++b)
                Assert.That(Decode(QuantizeForNetwork(Encode(b))), Is.EqualTo(b), "key byte " + b);
        }

        [Test]
        public void KeyBytes_NeverExceedMaxKeyByte()
        {
            foreach (byte b in TestKeys.UserKey.GetKeyBytes())
                Assert.That(b, Is.LessThanOrEqualTo(UserKey.MaxKeyByte));
        }
    }
}
#endif
