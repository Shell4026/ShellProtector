#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Shell.Protector;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace Shell.Protector.Tests.Integration
{
    public class SupportedShaderRenderingTests
    {
        private const int TextureSize = 128;
        private const int RenderSize = TextureSize;
        private const int RenderLayer = 30;
        private const byte ChannelTolerance = 16;
        private const double AverageChannelTolerance = 5.0;
        private const double CorruptedAverageChannelDifference = 20.0;
        private readonly List<Object> sceneObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            TestAssetScope.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            TestAssetScope.DestroyObjects(sceneObjects);
            TestAssetScope.DeleteGeneratedRoot();
            TestAssetScope.DeleteDefaultGeneratedRoot();
        }

        [Test]
        public void LilToonEncryptedMaterial_RendersLikeOriginal()
        {
            Shader shader = FindSupportedShader("lilToon");
            Fixture fixture = CreateFixture("LilToonSmoke", shader);
            Color32[] reference = RenderMaterial(fixture.Material);

            GameObject encryptedAvatar = fixture.Protector.Encrypt(false);
            sceneObjects.Add(encryptedAvatar);

            Material encryptedMaterial = GetBodyMaterial(encryptedAvatar);
            AssertEncryptedMaterial(encryptedMaterial);
            Assert.That(AssetDatabase.GetAssetPath(encryptedMaterial.shader).Replace('\\', '/'), Does.Contain("liltoonProtector/Shaders"));

            Color32[] actual = RenderMaterial(encryptedMaterial);
            AssertRenderedRgbClose(reference, actual, "lilToon");
        }

        [Test]
        public void LilToonEncryptedMaterial_WithTamperedKey_RendersCorruptedOutput()
        {
            Shader shader = FindSupportedShader("lilToon");
            Fixture fixture = CreateFixture("LilToonTamperedKey", shader);
            Color32[] reference = RenderMaterial(fixture.Material);

            GameObject encryptedAvatar = fixture.Protector.Encrypt(false);
            sceneObjects.Add(encryptedAvatar);

            Material encryptedMaterial = GetBodyMaterial(encryptedAvatar);
            AssertEncryptedMaterial(encryptedMaterial);
            Assert.That(AssetDatabase.GetAssetPath(encryptedMaterial.shader).Replace('\\', '/'), Does.Contain("liltoonProtector/Shaders"));

            AssertTamperedKeyRendersCorruptedOutput(encryptedMaterial, reference, "lilToon");
        }

        [Test]
        public void PoiyomiEncryptedMaterial_RendersLikeOriginal()
        {
            Shader shader = FindSupportedShader(".poiyomi/Poiyomi Toon");
            Fixture fixture = CreateFixture("PoiyomiSmoke", shader);
            Color32[] reference = RenderMaterial(fixture.Material);

            GameObject encryptedAvatar = fixture.Protector.Encrypt(false);
            sceneObjects.Add(encryptedAvatar);

            Material encryptedMaterial = GetBodyMaterial(encryptedAvatar);
            AssertEncryptedMaterial(encryptedMaterial);
            AssertPoiyomiShaderWasInjected(encryptedMaterial.shader);

            Color32[] actual = RenderMaterial(encryptedMaterial);
            AssertRenderedRgbClose(reference, actual, "Poiyomi");
        }

        [Test]
        public void PoiyomiEncryptedMaterial_WithTamperedKey_RendersCorruptedOutput()
        {
            Shader shader = FindSupportedShader(".poiyomi/Poiyomi Toon");
            Fixture fixture = CreateFixture("PoiyomiTamperedKey", shader);
            Color32[] reference = RenderMaterial(fixture.Material);

            GameObject encryptedAvatar = fixture.Protector.Encrypt(false);
            sceneObjects.Add(encryptedAvatar);

            Material encryptedMaterial = GetBodyMaterial(encryptedAvatar);
            AssertEncryptedMaterial(encryptedMaterial);
            AssertPoiyomiShaderWasInjected(encryptedMaterial.shader);

            AssertTamperedKeyRendersCorruptedOutput(encryptedMaterial, reference, "Poiyomi");
        }

        private Fixture CreateFixture(string name, Shader shader)
        {
            Texture2D texture = CreateSrgbPatternTexture(TextureSize, TextureSize, true);
            texture.name = name + "Texture";
            string texturePath = TestAssetScope.CreateAsset(texture, name + "/texture.asset");
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            Material material = new Material(shader);
            material.name = name + "Material";
            material.mainTexture = texture;
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);

            string materialPath = TestAssetScope.CreateAsset(material, name + "/material.mat");
            material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            Mesh mesh = TestAssetScope.CreateBlendShapeQuadMesh();
            string meshPath = TestAssetScope.CreateAsset(mesh, name + "/mesh.asset");
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            AnimatorController fx = CreateFxController(name);
            ScriptableObject parameters = CreateExpressionParameters(name);

            GameObject avatar = new GameObject(name + "Avatar");
            sceneObjects.Add(avatar);

            GameObject body = new GameObject("Body");
            body.transform.SetParent(avatar.transform, false);
            SkinnedMeshRenderer renderer = body.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = material;

            VRCAvatarDescriptor descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
            VrcExpressionParametersTestUtil.SetDescriptorParameters(descriptor, parameters);
            descriptor.VisemeBlendShapes = Enumerable.Repeat(string.Empty, (int)VRC_AvatarDescriptor.Viseme.Count).ToArray();
            var eyeSettings = descriptor.customEyeLookSettings;
            eyeSettings.eyelidsBlendshapes = new int[0];
            descriptor.customEyeLookSettings = eyeSettings;
            descriptor.baseAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[5];
            descriptor.baseAnimationLayers[4] = new VRCAvatarDescriptor.CustomAnimLayer
            {
                type = VRCAvatarDescriptor.AnimLayerType.FX,
                isDefault = false,
                animatorController = fx
            };

            ShellProtector protector = avatar.AddComponent<ShellProtector>();
            protector.Descriptor = descriptor;
            protector.AssetDir = TestAssetScope.GeneratedRoot;
            SetSerializedField(protector, "_gameObjectList", new List<GameObject> { avatar });
            SetSerializedField(protector, "_algorithm", (int)ShellProtectorAlgorithm.Chacha);
            SetSerializedField(protector, "_filter", (int)ShellProtectorTextureFilter.Bilinear);
            SetSerializedField(protector, "_fallback", (int)ShellProtectorFallback.Size32);
            SetSerializedField(protector, "_keySize", 12);
            SetSerializedField(protector, "_syncSize", 1);
            protector.Init();

            return new Fixture
            {
                Avatar = avatar,
                Protector = protector,
                Material = material
            };
        }

        private static Shader FindSupportedShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                Assert.Ignore(shaderName + " shader was not found.");
            if (!shader.isSupported)
                Assert.Ignore(shaderName + " shader is not supported or failed to compile.");
            return shader;
        }

        private static Texture2D CreateSrgbPatternTexture(int width, int height, bool alpha)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true, false);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte r = (byte)((x * 17 + y * 3) & 0xFF);
                    byte g = (byte)((x * 5 + y * 29) & 0xFF);
                    byte b = (byte)((x * 11 + y * 7 + 31) & 0xFF);
                    byte a = alpha ? (byte)(64 + ((x * 13 + y * 19) & 0x7F)) : (byte)255;
                    pixels[y * width + x] = new Color32(r, g, b, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Repeat;
            return texture;
        }

        private static AnimatorController CreateFxController(string name)
        {
            string dir = TestAssetScope.GeneratedRoot + "/" + name;
            TestAssetScope.EnsureFolder(dir);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(dir + "/fx.controller");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(dir + "/fx.controller");
        }

        private static ScriptableObject CreateExpressionParameters(string name)
        {
            ScriptableObject parameters = VrcExpressionParametersTestUtil.Create(
                name + "Params",
                new VrcExpressionParametersTestUtil.ParameterSpec
                {
                    Name = "existing",
                    Saved = false,
                    NetworkSynced = false,
                    ValueType = "Bool",
                    DefaultValue = 0f
                });
            string path = TestAssetScope.CreateAsset(parameters, name + "/params.asset");
            return AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        }

        private static Material GetBodyMaterial(GameObject avatar)
        {
            Assert.That(avatar, Is.Not.Null);
            Transform body = avatar.transform.Find("Body");
            Assert.That(body, Is.Not.Null);
            SkinnedMeshRenderer renderer = body.GetComponent<SkinnedMeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            return renderer.sharedMaterial;
        }

        private static void AssertEncryptedMaterial(Material material)
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(material.shader.isSupported, Is.True, "Encrypted shader should be supported: " + material.shader.name);
            Assert.That(material.GetTexture(ShaderProperties.EncryptTexture0), Is.Not.Null);
            Assert.That(material.GetTexture(ShaderProperties.MipTexture), Is.Not.Null);
            Assert.That(material.IsKeywordEnabled(ShaderProperties.ChachaKeyword), Is.True, "Encrypted material should enable Chacha keyword.");
            Assert.That(material.IsKeywordEnabled(ShaderProperties.Format1Keyword), Is.True, "Encrypted material should enable RGBA format keyword.");
            Assert.That(material.IsKeywordEnabled(ShaderProperties.Format0Keyword), Is.False, "Encrypted material should not enable RGB format keyword.");
        }

        private static void AssertPoiyomiShaderWasInjected(Shader shader)
        {
            string path = AssetDatabase.GetAssetPath(shader).Replace('\\', '/');
            Assert.That(path, Does.StartWith(TestAssetScope.GeneratedRoot));
            string shaderData = File.ReadAllText(path);
            Assert.That(shaderData, Does.Contain("//ShellProtect"));
            Assert.That(shaderData, Does.Contain("Protector.cginc"));
            Assert.That(shaderData, Does.Contain(ShaderProperties.EncryptTexture0));
            Assert.That(shaderData, Does.Contain(ShaderProperties.MipTexture));
        }

        private static void AssertTamperedKeyRendersCorruptedOutput(Material encryptedMaterial, Color32[] reference, string label)
        {
            Material fallbackProbe = null;
            Material tamperedProbe = null;

            try
            {
                fallbackProbe = CreateFallbackProbe(encryptedMaterial);
                tamperedProbe = CreateTamperedKeyProbe(encryptedMaterial);

                Color32[] fallback = RenderMaterial(fallbackProbe);
                Color32[] tampered = RenderMaterial(tamperedProbe);

                AssertRenderedRgbDifferent(reference, tampered, label + " tampered output should differ from the original");
                AssertRenderedRgbDifferent(fallback, tampered, label + " tampered output should differ from fallback");
            }
            finally
            {
                if (fallbackProbe != null)
                    Object.DestroyImmediate(fallbackProbe);
                if (tamperedProbe != null)
                    Object.DestroyImmediate(tamperedProbe);
            }
        }

        private static Material CreateFallbackProbe(Material encryptedMaterial)
        {
            Material fallbackProbe = new Material(encryptedMaterial);
            fallbackProbe.name = encryptedMaterial.name + "FallbackProbe";
            fallbackProbe.SetInteger(ShaderProperties.PasswordHash, fallbackProbe.GetInteger(ShaderProperties.PasswordHash) ^ 0x5A5A5A5A);
            return fallbackProbe;
        }

        private static Material CreateTamperedKeyProbe(Material encryptedMaterial)
        {
            Material tamperedProbe = new Material(encryptedMaterial);
            tamperedProbe.name = encryptedMaterial.name + "TamperedKeyProbe";

            byte[] key = ReadKeyBytes(tamperedProbe);
            key[0] = (byte)(key[0] ^ 0x5A);
            ApplyKeyBytes(tamperedProbe, key);
            return tamperedProbe;
        }

        private static byte[] ReadKeyBytes(Material material)
        {
            byte[] key = new byte[16];
            for (int i = 0; i < key.Length; i++)
                key[i] = (byte)Mathf.RoundToInt(material.GetFloat(ShaderProperties.KeyPrefix + i));
            return key;
        }

        private static void ApplyKeyBytes(Material material, byte[] key)
        {
            for (int i = 0; i < key.Length; i++)
                material.SetFloat(ShaderProperties.KeyPrefix + i, key[i]);

            uint hashMagic = unchecked((uint)material.GetInteger(ShaderProperties.HashMagic));
            uint passwordHash = KeyGenerator.SimpleHash(key, hashMagic);
            material.SetInteger(ShaderProperties.PasswordHash, unchecked((int)passwordHash));
        }

        private static Color32[] RenderMaterial(Material material)
        {
            GameObject subject = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            Camera camera = null;
            RenderTexture target = null;
            Texture2D readback = null;
            RenderTexture previous = RenderTexture.active;
            AmbientMode previousAmbientMode = RenderSettings.ambientMode;
            Color previousAmbientLight = RenderSettings.ambientLight;

            try
            {
                subject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                subject.layer = RenderLayer;
                subject.transform.position = Vector3.zero;
                subject.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
                Object.DestroyImmediate(subject.GetComponent<Collider>());
                MeshRenderer renderer = subject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                cameraObject = new GameObject("ShellProtectorRenderCamera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 0f, -2f);
                cameraObject.transform.rotation = Quaternion.identity;
                camera.orthographic = true;
                camera.orthographicSize = 0.55f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << RenderLayer;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;

                lightObject = new GameObject("ShellProtectorRenderLight");
                lightObject.layer = RenderLayer;
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = Color.white;
                light.intensity = 1f;
                light.cullingMask = 1 << RenderLayer;
                lightObject.transform.rotation = Quaternion.Euler(35f, 30f, 0f);

                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.white;

                target = new RenderTexture(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                target.Create();
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                readback = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false, true);
                readback.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
                readback.Apply(false, false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
                RenderTexture.active = previous;

                if (camera != null)
                    camera.targetTexture = null;
                if (target != null)
                    target.Release();
                if (target != null)
                    Object.DestroyImmediate(target);
                if (readback != null)
                    Object.DestroyImmediate(readback);
                if (subject != null)
                    Object.DestroyImmediate(subject);
                if (cameraObject != null)
                    Object.DestroyImmediate(cameraObject);
                if (lightObject != null)
                    Object.DestroyImmediate(lightObject);
            }
        }

        private static void AssertRenderedRgbClose(Color32[] expected, Color32[] actual, string label)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));

            int compared = 0;
            long totalDifference = 0;
            for (int y = 8; y < RenderSize - 8; y++)
            {
                for (int x = 8; x < RenderSize - 8; x++)
                {
                    int i = y * RenderSize + x;
                    AssertChannelClose(expected[i].r, actual[i].r, label, i, "r");
                    AssertChannelClose(expected[i].g, actual[i].g, label, i, "g");
                    AssertChannelClose(expected[i].b, actual[i].b, label, i, "b");
                    totalDifference += AbsDiff(expected[i].r, actual[i].r);
                    totalDifference += AbsDiff(expected[i].g, actual[i].g);
                    totalDifference += AbsDiff(expected[i].b, actual[i].b);
                    compared += 3;
                }
            }

            double averageDifference = (double)totalDifference / compared;
            Assert.That(averageDifference, Is.LessThanOrEqualTo(AverageChannelTolerance), label + " average RGB difference");
        }

        private static void AssertRenderedRgbDifferent(Color32[] expected, Color32[] actual, string label)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            double averageDifference = CalculateAverageRgbDifference(expected, actual);
            Assert.That(averageDifference, Is.GreaterThanOrEqualTo(CorruptedAverageChannelDifference), label + " average RGB difference");
        }

        private static double CalculateAverageRgbDifference(Color32[] expected, Color32[] actual)
        {
            long totalDifference = 0;
            int compared = 0;
            for (int y = 8; y < RenderSize - 8; y++)
            {
                for (int x = 8; x < RenderSize - 8; x++)
                {
                    int i = y * RenderSize + x;
                    totalDifference += AbsDiff(expected[i].r, actual[i].r);
                    totalDifference += AbsDiff(expected[i].g, actual[i].g);
                    totalDifference += AbsDiff(expected[i].b, actual[i].b);
                    compared += 3;
                }
            }

            return (double)totalDifference / compared;
        }

        private static void AssertChannelClose(byte expected, byte actual, string label, int pixel, string channel)
        {
            int difference = AbsDiff(expected, actual);
            if (difference > ChannelTolerance)
                Assert.Fail("{0} pixel {1} channel {2} differs. Expected {3}, Actual {4}, Difference {5}", label, pixel, channel, expected, actual, difference);
        }

        private static int AbsDiff(byte a, byte b)
        {
            return a > b ? a - b : b - a;
        }

        private static void SetSerializedField<T>(ShellProtector protector, string fieldName, T value)
        {
            FieldInfo field = typeof(ShellProtector).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(protector, value);
        }

        private class Fixture
        {
            public GameObject Avatar;
            public ShellProtector Protector;
            public Material Material;
        }
    }
}
#endif
