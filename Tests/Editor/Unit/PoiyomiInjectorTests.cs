#if UNITY_EDITOR
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shell.Protector.Tests.Unit
{
    public class PoiyomiInjectorTests
    {
        private const string WrappedFunction = @"
float RTWrapFunc(in float dt, in float w, in float norm)
{
    float cw = saturate(w);
    float o = (dt + cw) / ((1.0 + cw) * (1.0 + cw * norm));
    float flt = 1.0 - 0.85 * norm;
    if (w > 1.0)
    {
        o = lerp(o, flt, w - 1.0);
    }
	return o;
}";

        private const string VertexFunction = @"
VertexOut vert(appdata v)
{
    VertexOut o;
    PoiInitStruct(VertexOut, o);
    // A comment containing a closing brace: }
    /* A comment containing an opening brace: { */
    if (v.vertex.x > 0)
    {
        o.pos = v.vertex;
        return o;
    }
	return o;
}";

        [Test]
        public void InjectVertexDecryptionState_PreservesWrappedLightingAndOtherReturns()
        {
            const string otherFunction = "float4 other() { float4 o = 0; return o; }";
            string source = WrappedFunction + VertexFunction + WrappedFunction + otherFunction;

            string actual = Inject(source);

            Assert.That(Regex.Matches(actual, Regex.Escape(WrappedFunction)).Count, Is.EqualTo(2));
            Assert.That(actual, Does.EndWith(otherFunction));
            Assert.That(Regex.Matches(actual, @"o\.isDecrypted = IsDecrypted\(\);").Count, Is.EqualTo(2));
        }

        [TestCase("\n")]
        [TestCase("\r\n")]
        public void InjectVertexDecryptionState_HandlesEveryPassAndEarlyReturn(string newline)
        {
            string source = (VertexFunction + WrappedFunction + VertexFunction).Replace("\r\n", "\n").Replace("\n", newline);

            string actual = Inject(source);

            Assert.That(Regex.Matches(actual, @"o\.isDecrypted = IsDecrypted\(\);\s*return\s+o\s*;").Count,
                Is.EqualTo(4), "Each vertex return, including early returns, must carry the decryption state.");
        }

        [Test]
        public void InjectVertexDecryptionState_WithoutVertexFunction_DoesNotModifySource()
        {
            Assert.That(Inject(WrappedFunction), Is.EqualTo(WrappedFunction));
        }

        private static string Inject(string source)
        {
            MethodInfo method = typeof(PoiyomiInjector).GetMethod("InjectVertexDecryptionState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { source });
        }
    }

    public class InjectorTests
    {
        [TestCase(null, false)]
        [TestCase(0, false)]
        [TestCase(Injector.CurrentInjectedShaderVersion, true)]
        public void CanReuseShader_RequiresTheCurrentInjectedVersion(int? version, bool expected)
        {
            string property = version.HasValue
                ? "[HideInInspector] " + ShaderProperties.InjectedShaderVersion + " (\"Injected version\", Integer) = " + version.Value
                : "";
            Shader shader = ShaderUtil.CreateShaderAsset("Shader \"Hidden/ShellInjectedVersionTest\" { Properties { " + property + " } SubShader { Pass { } } }", false);
            try
            {
                Assert.That(new PoiyomiInjector().CanReuseShader(shader), Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(shader);
            }
        }

        [TestCase(false, ShellProtectorTextureFilter.Point, true)]
        [TestCase(false, ShellProtectorTextureFilter.Bilinear, false)]
        [TestCase(true, ShellProtectorTextureFilter.Point, true)]
        [TestCase(true, ShellProtectorTextureFilter.Bilinear, false)]
        public void SetKeywords_SelectsThePointFilterWithAKeyword(bool lilToon, ShellProtectorTextureFilter filter, bool point)
        {
            var material = new Material(Shader.Find("Hidden/GpuDecryptTest"));
            try
            {
                Injector injector = lilToon ? new LilToonInjector() : new PoiyomiInjector();
                injector.Init(null, null, new byte[16], 12, (int)filter, "Assets/ShellProtector/Runtime");
                injector.SetKeywords(material);
                Assert.That(material.IsKeywordEnabled(ShaderProperties.PointKeyword), Is.EqualTo(point));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
#endif
