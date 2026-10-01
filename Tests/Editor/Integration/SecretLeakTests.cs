#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shell.Protector.Diagnostics;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace Shell.Protector.Tests.Integration
{
    // What a build hands to VRChat must hold neither password nor the user part of the key.
    // The fixed key bytes and the password hash are stored in materials by design and are not checked here.
    public class SecretLeakTests
    {
        const string FixedPassword = "Qx7#";
        const string UserPassword = "Us3r!Secr3t9";
        const int UserKeyLength = 12; // Set by SupportedShaderRenderingTests.CreateFixture.
        const int FixedKeyLength = 16 - UserKeyLength;

        SupportedShaderRenderingTests fixtureOwner;
        GameObject encryptedAvatar;
        readonly List<string> logs = new List<string>();

        [SetUp]
        public void SetUp()
        {
            fixtureOwner = new SupportedShaderRenderingTests();
            fixtureOwner.SetUp();
            logs.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= Record;
            if (encryptedAvatar != null) Object.DestroyImmediate(encryptedAvatar);
            fixtureOwner.TearDown();
        }

        [TestCase("lilToon", false)] [TestCase("lilToon", true)]
        [TestCase(".poiyomi/Poiyomi Toon", false)] [TestCase(".poiyomi/Poiyomi Toon", true)]
        public void NdmfBuildOutputHoldsNoPasswordOrUserKey(string shaderName, bool bc7)
        {
            var fixture = CreateFixture("LeakNdmf", shaderName, bc7);
            byte[] key = fixture.Protector.GetKeyBytes();

            Application.logMessageReceived += Record;
            Type processor = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("nadena.dev.ndmf.AvatarProcessor")).FirstOrDefault(t => t != null);
            Assert.That(processor, Is.Not.Null, "The validation project must include NDMF.");
            processor.GetMethod("ProcessAvatar", new[] { typeof(GameObject) }).Invoke(null, new object[] { fixture.Avatar });
            Application.logMessageReceived -= Record;

            Assert.That(fixture.Avatar.GetComponentsInChildren<ShellProtector>(true), Is.Empty, "The build must remove the protector.");
            AssertNoSecrets(fixture.Avatar, key);
        }

        [TestCase("lilToon", false)] [TestCase("lilToon", true)]
        [TestCase(".poiyomi/Poiyomi Toon", false)] [TestCase(".poiyomi/Poiyomi Toon", true)]
        public void ManualOutputHoldsNoUserKeyBeforeTheTesterOrAfterItsReset(string shaderName, bool bc7)
        {
            var fixture = CreateFixture("LeakManual", shaderName, bc7);
            byte[] key = fixture.Protector.GetKeyBytes();

            Application.logMessageReceived += Record;
            encryptedAvatar = fixture.Protector.Encrypt(false);
            Application.logMessageReceived -= Record;
            AssertNoSecrets(encryptedAvatar, key);

            // The tester writes the whole key for a local preview; "Done & Reset" must clear it before upload.
            var tester = encryptedAvatar.GetComponentInChildren<ShellProtectorTester>(true);
            Assert.That(tester, Is.Not.Null);
            tester.CheckEncryption();
            tester.ResetEncryption();
            AssertNoSecrets(encryptedAvatar, key);
        }

        SupportedShaderRenderingTests.Fixture CreateFixture(string name, string shaderName, bool bc7)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                Assert.Ignore(shaderName + " is not installed.");
            var fixture = fixtureOwner.CreateFixture(name, shader, bc7 ? TextureDiagnostics.Pattern(128, 128, true, true) : null);
            SupportedShaderRenderingTests.SetSerializedField(fixture.Protector, "_fixedPassword", FixedPassword);
            SupportedShaderRenderingTests.SetSerializedField(fixture.Protector, "_userPassword", UserPassword);
            return fixture;
        }

        void Record(string message, string stackTrace, LogType type) => logs.Add(message);

        void AssertNoSecrets(GameObject avatar, byte[] key)
        {
            byte[] userKey = key.Skip(FixedKeyLength).ToArray();
            string[] secrets = { FixedPassword, UserPassword, string.Join(", ", key), string.Join(", ", userKey) };
            // Unity YAML stores raw texture and mesh bytes as lowercase hex.
            string[] hexSecrets = { Hex(key), Hex(userKey) };

            var materials = new HashSet<Material>();
            foreach (Object obj in UploadedObjects(avatar))
            {
                string json = EditorJsonUtility.ToJson(obj);
                foreach (string secret in secrets)
                    Assert.That(json, Does.Not.Contain(secret), Describe(obj) + " serializes a password or key.");
                if (obj is Material material)
                    materials.Add(material);
            }

            foreach (string path in Directory.GetFiles(TestAssetScope.GeneratedRoot, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")))
            {
                string text = File.ReadAllText(path);
                foreach (string secret in secrets.Concat(hexSecrets))
                    Assert.That(text, Does.Not.Contain(secret), path + " stores a password or key.");
                if (path.EndsWith(".mat"))
                    materials.Add(AssetDatabase.LoadAssetAtPath<Material>(path.Replace('\\', '/')));
                if (path.EndsWith(".shader"))
                    foreach (Match match in Regex.Matches(text, @"_Key\d+\s*\(""[^""]*"",\s*float\)\s*=\s*([^\s]+)"))
                        Assert.That(match.Groups[1].Value, Is.EqualTo("0"), path + " bakes a key byte into a shader default.");
            }

            var encrypted = materials.Where(m => m != null && m.HasProperty(ShaderProperties.KeyPrefix + "0")).ToArray();
            Assert.That(encrypted, Is.Not.Empty, "The scan must reach the encrypted materials.");
            foreach (Material material in encrypted)
                for (int i = FixedKeyLength; i < 16; ++i)
                    Assert.That(material.GetFloat(ShaderProperties.KeyPrefix + i), Is.Zero, material.name + " stores user key byte " + i + ".");

            foreach (string log in logs)
                foreach (string secret in secrets)
                    Assert.That(log, Does.Not.Contain(secret), "A build log prints a password or key.");
        }

        // VRChat strips IEditorOnly components and EditorOnly-tagged objects before upload.
        static IEnumerable<Object> UploadedObjects(GameObject avatar)
        {
            Object[] roots = avatar.GetComponentsInChildren<Component>(true)
                .Where(c => c != null && !(c is IEditorOnly) && !IsEditorOnly(c.transform))
                .Cast<Object>().ToArray();
            return EditorUtility.CollectDependencies(roots)
                .Where(o => o != null && !(o is IEditorOnly) && !(o is Component c && IsEditorOnly(c.transform)));
        }

        static bool IsEditorOnly(Transform transform)
        {
            for (; transform != null; transform = transform.parent)
                if (transform.CompareTag("EditorOnly"))
                    return true;
            return false;
        }

        static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        static string Describe(Object obj)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            return obj.GetType().Name + " '" + obj.name + "'" + (string.IsNullOrEmpty(path) ? "" : " (" + path + ")");
        }
    }
}
#endif
