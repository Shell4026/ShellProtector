#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shell.Protector.Tests.Integration
{
    public class Bc7PipelineTests
    {
        SupportedShaderRenderingTests fixtureOwner;
        GameObject encryptedAvatar;
        [SetUp] public void SetUp() { fixtureOwner = new SupportedShaderRenderingTests(); fixtureOwner.SetUp(); }
        [TearDown] public void TearDown()
        {
            if (encryptedAvatar != null) Object.DestroyImmediate(encryptedAvatar);
            fixtureOwner.TearDown();
        }

        [TestCase("lilToon", false, false)] [TestCase("lilToon", false, true)]
        [TestCase("lilToon", true, false)] [TestCase("lilToon", true, true)]
        [TestCase(".poiyomi/Poiyomi Toon", false, false)] [TestCase(".poiyomi/Poiyomi Toon", false, true)]
        [TestCase(".poiyomi/Poiyomi Toon", true, false)] [TestCase(".poiyomi/Poiyomi Toon", true, true)]
        public void Bc7MaterialRendersThroughManualAndNdmfPipeline(string shaderName, bool inPlace, bool bilinear)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, "Required installed shader: " + shaderName);
            var source = Bc7TestData.Pattern(128, 128, true, true);
            source.filterMode = bilinear ? FilterMode.Bilinear : FilterMode.Point;
            var fixture = fixtureOwner.CreateFixture("BC7Pipeline", shader, source);
            SupportedShaderRenderingTests.SetSerializedField(fixture.Protector, "_filter", bilinear ? 1 : 0);
            fixture.Material.mainTextureScale = new Vector2(1.07f, 1.07f);
            fixture.Material.mainTextureOffset = new Vector2(-.0192f, .0147f);
            Color32[] before = SupportedShaderRenderingTests.RenderMaterial(fixture.Material);
            byte[] key = fixture.Protector.GetKeyBytes();
            GameObject avatar;
            if (inPlace)
            {
                avatar = fixture.Avatar;
                Type processor = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("nadena.dev.ndmf.AvatarProcessor")).FirstOrDefault(t => t != null);
                Assert.That(processor, Is.Not.Null, "The isolated validation project must include NDMF.");
                processor.GetMethod("ProcessAvatar", new[] { typeof(GameObject) }).Invoke(null, new object[] { avatar });
            }
            else avatar = encryptedAvatar = fixture.Protector.Encrypt(false);
            Material material = SupportedShaderRenderingTests.GetBodyMaterial(avatar);
            for (int i = 0; i < key.Length; ++i) material.SetFloat("_Key" + i, key[i]);
            Assert.That(material.GetInteger(ShaderProperties.BC7LayoutVersion), Is.EqualTo(1));
            Color32[] after = SupportedShaderRenderingTests.RenderMaterial(material);
            long difference = 0;
            for (int i = 0; i < before.Length; ++i)
                difference += Math.Abs(before[i].r - after[i].r) + Math.Abs(before[i].g - after[i].g) + Math.Abs(before[i].b - after[i].b);
            if (difference / (double)(before.Length * 3) > 5)
            {
                Directory.CreateDirectory("Temp/BC7Integration");
                var image = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
                image.SetPixels32(before); File.WriteAllBytes("Temp/BC7Integration/before.png", image.EncodeToPNG());
                image.SetPixels32(after); File.WriteAllBytes("Temp/BC7Integration/after.png", image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            uint actualHash = KeyGenerator.SimpleHash(key, unchecked((uint)material.GetInteger(ShaderProperties.HashMagic)));
            Assert.That(difference / (double)(before.Length * 3), Is.LessThanOrEqualTo(5),
                "Integrated shader mean RGB error. hash=" + actualHash + "/" + unchecked((uint)material.GetInteger(ShaderProperties.PasswordHash)) +
                " shader=" + material.shader.name + " keywords=" + string.Join(",", material.shaderKeywords));
            var atlas = (Texture2D)material.GetTexture(ShaderProperties.EncryptTexture0);
            Assert.That(atlas.GetRawTextureData().Length, Is.LessThan(128 * 128 * 4));
        }

        [Test]
        public void PoiyomiSharedTextureHonorsDifferentFiltersWhenShaderIsCached()
        {
            var source = new Texture2D(128, 128, TextureFormat.RGBA32, true, false);
            var checker = new Color32[128 * 128];
            for (int i = 0; i < checker.Length; ++i)
                checker[i] = ((i + i / 128) & 1) == 0 ? Color.black : Color.white;
            source.SetPixels32(checker); source.Apply(true, false);
            EditorUtility.CompressTexture(source, TextureFormat.BC7, TextureCompressionQuality.Fast);
            source.Apply(false, false);
            var fixture = fixtureOwner.CreateFixture("BC7FilterCache", Shader.Find(".poiyomi/Poiyomi Toon"), source);
            fixture.Material.mainTextureScale = new Vector2(1.17f, .93f);
            fixture.Material.mainTextureOffset = new Vector2(-.031f, .023f);
            source.filterMode = FilterMode.Point;
            Color32[] point = SupportedShaderRenderingTests.RenderMaterial(fixture.Material);
            source.filterMode = FilterMode.Bilinear;
            Color32[] bilinear = SupportedShaderRenderingTests.RenderMaterial(fixture.Material);
            AssetManager.GetInstance().LockShader(fixture.Material);
            var second = new Material(fixture.Material) { name = "BC7Bilinear" };
            TestAssetScope.CreateAsset(second, "BC7FilterCache/bilinear.mat");
            fixture.Avatar.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials = new[] { fixture.Material, second };
            fixture.Protector.MaterialOptions[fixture.Material] = new ShellProtector.MatOption { Filter = 0, Fallback = (int)ShellProtectorFallback.Size32 };
            fixture.Protector.MaterialOptions[second] = new ShellProtector.MatOption { Filter = 1, Fallback = (int)ShellProtectorFallback.Size32 };
            encryptedAvatar = fixture.Protector.Encrypt(false);
            SupportedShaderRenderingTests.GetBodyMaterial(encryptedAvatar); // Activate the manual tester's user key.
            Material[] output = encryptedAvatar.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials;
            // Use the existing full-shader RGB tolerance; codec/sampler tests use the stricter byte contract.
            Assert.That(RgbError(point, SupportedShaderRenderingTests.RenderMaterial(output[0])), Is.LessThanOrEqualTo(5));
            Assert.That(RgbError(bilinear, SupportedShaderRenderingTests.RenderMaterial(output[1])), Is.LessThanOrEqualTo(5));
        }

        static double RgbError(Color32[] expected, Color32[] actual)
        {
            long error = 0;
            for (int i = 0; i < expected.Length; ++i)
                error += Math.Abs(expected[i].r - actual[i].r) + Math.Abs(expected[i].g - actual[i].g) + Math.Abs(expected[i].b - actual[i].b);
            return error / (double)(expected.Length * 3);
        }

        [Test]
        public void SharedSourceRetainsOneAtlasAndNonceWhileAnotherSourceIsIndependent()
        {
            var source = Bc7TestData.Pattern(128, 128, true, true);
            var fixture = fixtureOwner.CreateFixture("BC7Sharing", Shader.Find("lilToon"), source);
            var shared = new Material(fixture.Material);
            TestAssetScope.CreateAsset(shared, "BC7Sharing/shared.mat");
            var separate = new Material(fixture.Material);
            var rectangle = Bc7TestData.Pattern(128, 64, true, true);
            TestAssetScope.CreateAsset(rectangle, "BC7Sharing/rectangle.asset");
            separate.mainTexture = rectangle;
            TestAssetScope.CreateAsset(separate, "BC7Sharing/separate.mat");
            fixture.Avatar.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials = new[] { fixture.Material, shared, separate };
            encryptedAvatar = fixture.Protector.Encrypt(false);
            Material[] output = encryptedAvatar.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials;
            Assert.That(output[0].GetTexture(ShaderProperties.EncryptTexture0), Is.SameAs(output[1].GetTexture(ShaderProperties.EncryptTexture0)));
            int[] Nonce(Material m) => Enumerable.Range(0, 3).Select(i => m.GetInteger("_Nonce" + i)).ToArray();
            Assert.That(Nonce(output[0]), Is.EqualTo(Nonce(output[1])));
            Assert.That(Nonce(output[0]), Is.Not.EqualTo(Nonce(output[2])));
            Assert.That(output[2].GetVector(ShaderProperties.SourceTexelSize).w, Is.EqualTo(64));
        }

        [Test]
        public void SavedMaterialAndCiphertextSurviveAssetBundleRoundTrip()
        {
            var source = Bc7TestData.Pattern(64, 32, true, true);
            var cipher = new Chacha20();
            var encrypted = TextureEncryptManager.EncryptTexture(source, Bc7TestData.Key, cipher);
            var material = Bc7TestData.Material(source, encrypted);
            var mip = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            mip.SetPixel(0, 0, Color.black); mip.Apply(false, false); material.SetTexture("_MipTex", mip);
            TestAssetScope.CreateAsset(encrypted.Texture1, "Bundle/atlas.asset");
            TestAssetScope.CreateAsset(mip, "Bundle/mip.asset");
            string materialPath = TestAssetScope.CreateAsset(material, "Bundle/material.mat");
            byte[] ciphertext = encrypted.Texture1.GetRawTextureData();
            var expected = Bc7TestData.Render(Texture2D.blackTexture, material, 1, 64, 32);
            string output = Path.GetFullPath("Temp/BC7BundleValidation"); Directory.CreateDirectory(output);
            AssetBundle bundle = null;
            try
            {
                var manifest = BuildPipeline.BuildAssetBundles(output,
                    new[] {new AssetBundleBuild {assetBundleName = "bc7-test", assetNames = new[] {materialPath}}},
                    BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.StandaloneWindows64);
                Assert.That(manifest, Is.Not.Null);
                bundle = AssetBundle.LoadFromFile(Path.Combine(output, "bc7-test"));
                var restored = bundle.LoadAsset<Material>(materialPath);
                var restoredAtlas = (Texture2D)restored.GetTexture(ShaderProperties.EncryptTexture0);
                Assert.That(restoredAtlas.GetRawTextureData(), Is.EqualTo(ciphertext), "Ciphertext bytes must not be recompressed.");
                var actual = Bc7TestData.Render(Texture2D.blackTexture, restored, 1, 64, 32);
                Assert.That(Bc7TestData.MaxError(expected, actual), Is.Zero, "Serialized layout and shader variant must still decode.");
            }
            finally { if (bundle != null) bundle.Unload(true); Object.DestroyImmediate(source); }
        }
    }
}
#endif
