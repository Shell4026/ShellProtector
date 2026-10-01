#if UNITY_EDITOR
using System;
using Shell.Protector.Diagnostics;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shell.Protector.Tests.Gpu
{
    public class Bc7DecryptionTests
    {
        readonly List<Object> objects = new List<Object>();
        T Own<T>(T value) where T : Object { objects.Add(value); return value; }
        [TearDown] public void TearDown() { foreach (Object obj in objects) Object.DestroyImmediate(obj); objects.Clear(); }

        Material Prepare(Texture2D source, out Texture2D mip)
        {
            var cipher = new Chacha20();
            EncryptResult result = TextureEncryptManager.EncryptTexture(source, TextureDiagnostics.Key, cipher);
            Own(result.Texture1);
            var material = Own(Bc7TestData.Material(source, result));
            mip = Own(new Texture2D(1, 1, TextureFormat.RGBA32, false, true));
            mip.SetPixel(0, 0, Color.black); mip.Apply(false, false);
            material.SetTexture("_MipTex", mip);
            material.SetTexture("_LinearReference", Own(TextureDiagnostics.LinearCopy(source)));
            return material;
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void EveryModePartitionRotationSelectorAndPbitsMatchNativeGpu(int mode)
        {
            var source = Own(Bc7TestData.ModeTexture(mode));
            Material material = Prepare(source, out _);
            var expected = TextureDiagnostics.Render(source, material, 0, source.width, source.height);
            var actual = TextureDiagnostics.Render(Texture2D.blackTexture, material, 1, source.width, source.height);
            byte[] normalized = new byte[32];
            byte[] raw = source.GetRawTextureData();
            BC7Codec.Normalize(raw.AsSpan(0, 16), normalized);
            Assert.That(Bc7TestData.MaxError(expected, actual), Is.Zero,
                "First pixels: native=" + expected[0] + "," + expected[1] + " decrypted=" + actual[0] + "," + actual[1]
                + " raw=" + BitConverter.ToString(raw, 0, 16) + " record=" + BitConverter.ToString(normalized));
        }

        [TestCase(TextureWrapMode.Repeat, false)] [TestCase(TextureWrapMode.Repeat, true)]
        [TestCase(TextureWrapMode.Clamp, false)] [TestCase(TextureWrapMode.Clamp, true)]
        [TestCase(TextureWrapMode.Mirror, false)] [TestCase(TextureWrapMode.Mirror, true)]
        [TestCase(TextureWrapMode.MirrorOnce, false)] [TestCase(TextureWrapMode.MirrorOnce, true)]
        public void FractionalSamplingAndNegativeUvsMatchOriginal(TextureWrapMode wrap, bool srgb)
        {
            var source = Own(TextureDiagnostics.Pattern(64, 32, true, srgb));
            source.wrapMode = wrap;
            Material material = Prepare(source, out _);
            material.SetVector("_UVTransform", new Vector4(2.4f, 1.7f, -0.2137f, 1.1661f));
            foreach (bool bilinear in new[] { false, true })
            {
                source.filterMode = bilinear ? FilterMode.Bilinear : FilterMode.Point;
                material.SetInteger("_ReferenceBilinear", bilinear ? 1 : 0);
                var expected = TextureDiagnostics.Render(source, material, 0, 64, 32);
                var controlled = TextureDiagnostics.Render(source, material, 3, 64, 32);
                var actual = TextureDiagnostics.Render(Texture2D.blackTexture, material, bilinear ? 2 : 1, 64, 32);
                Assert.That(Bc7TestData.MaxError(controlled, actual), Is.Zero, "Explicit reference; bilinear=" + bilinear);
                Assert.That(Bc7TestData.MaxError(expected, actual), Is.LessThanOrEqualTo(srgb || bilinear ? 2 : 0), "Native sampler; bilinear=" + bilinear);
            }
        }

        [Test]
        public void RandomMixedModeBlocksMatchNativeGpu()
        {
            var random = new System.Random(270129);
            byte[] raw = new byte[256 * 256]; random.NextBytes(raw);
            for (int i = 0; i < raw.Length; i += 16)
            {
                int mode = random.Next(8);
                raw[i] = (byte)((raw[i] & ~((1 << (mode + 1)) - 1)) | (1 << mode));
            }
            var source = Own(new Texture2D(256, 256, TextureFormat.BC7, false, true));
            source.LoadRawTextureData(raw); source.Apply(false, false); source.filterMode = FilterMode.Point;
            Material material = Prepare(source, out _);
            var reference = TextureDiagnostics.Render(source, material, 0, 256, 256);
            var actual = TextureDiagnostics.Render(Texture2D.blackTexture, material, 1, 256, 256);
            Assert.That(Bc7TestData.MaxError(reference, actual), Is.Zero);
        }

        [Test]
        public void RectangularTextureAllMipLevelsIncludingTailMatchNativeGpu()
        {
            var source = Own(TextureDiagnostics.Pattern(128, 32, true, false)); source.filterMode = FilterMode.Point;
            Material material = Prepare(source, out Texture2D mipReference);
            for (int mip = 0; mip < source.mipmapCount; ++mip)
            {
                mipReference.SetPixel(0, 0, new Color32((byte)(mip * 10), 0, 0, 255)); mipReference.Apply(false, false);
                material.SetFloat("_Lod", mip);
                int w = Math.Max(1, source.width >> mip), h = Math.Max(1, source.height >> mip);
                var reference = TextureDiagnostics.Render(source, material, 0, w, h);
                var actual = TextureDiagnostics.Render(Texture2D.blackTexture, material, 1, w, h);
                Assert.That(Bc7TestData.MaxError(reference, actual), Is.Zero, "Mip " + mip);
            }
        }

        [Test]
        public void SrgbBilinearFullMipChainAndSinglePixelTailMatchControlledReference()
        {
            var source = Own(TextureDiagnostics.Pattern(128, 32, true, true)); source.filterMode = FilterMode.Bilinear;
            Material material = Prepare(source, out Texture2D mipReference);
            material.SetVector("_UVTransform", new Vector4(2.4f, 1.7f, -.2137f, 1.1661f));
            material.SetInteger("_ReferenceBilinear", 1);
            for (int mip = 0; mip < source.mipmapCount; ++mip)
            {
                mipReference.SetPixel(0, 0, new Color32((byte)(mip * 10), 0, 0, 255)); mipReference.Apply(false, false);
                material.SetFloat("_Lod", mip);
                var native = TextureDiagnostics.Render(source, material, 0, 64, 32);
                var controlled = TextureDiagnostics.Render(source, material, 3, 64, 32);
                var actual = TextureDiagnostics.Render(Texture2D.blackTexture, material, 2, 64, 32);
                Assert.That(Bc7TestData.MaxError(controlled, actual), Is.Zero, "Controlled mip " + mip);
                Assert.That(Bc7TestData.MaxError(native, actual), Is.LessThanOrEqualTo(2), "Native mip " + mip);
            }
        }

        [Test]
        public void ReservedModeDecodesToTransparentBlack()
        {
            var source = Own(new Texture2D(8, 8, TextureFormat.BC7, false, true));
            source.LoadRawTextureData(new byte[64]); source.Apply(false, false);
            Material material = Prepare(source, out _);
            var reference = TextureDiagnostics.Render(source, material, 0, 8, 8);
            var actual = TextureDiagnostics.Render(Texture2D.blackTexture, material, 1, 8, 8);
            Assert.That(Bc7TestData.MaxError(reference, actual), Is.Zero);
        }

        [Test]
        public void EncryptionResultStillRendersAfterTheCipherIsReused()
        {
            var source = Own(TextureDiagnostics.Pattern(64, 32, true, false));
            var cipher = new Chacha20();
            var first = TextureEncryptManager.EncryptTexture(source, TextureDiagnostics.Key, cipher);
            Own(first.Texture1);
            Own(TextureEncryptManager.EncryptTexture(source, TextureDiagnostics.Key, cipher).Texture1);
            Array.Clear(cipher.Nonce, 0, cipher.Nonce.Length);

            var mip = Own(new Texture2D(1, 1, TextureFormat.RGBA32, false, true));
            mip.SetPixel(0, 0, Color.black); mip.Apply(false, false);
            var original = Own(new Material(Shader.Find("Hidden/ShellProtector/BC7Test")));
            original.mainTexture = source;
            var injector = new LilToonInjector();
            injector.Init(null, source, TextureDiagnostics.Key, 12, 0, "Assets/ShellProtector/Runtime", cipher);
            var material = Own(MaterialEncryptor.CreateEncryptedMaterial(original, original.shader,
                Texture2D.blackTexture, mip, new AuxiliaryTextures(), first, TextureDiagnostics.Key, 16, false, injector));
            var expected = TextureDiagnostics.Render(source, material, 0, source.width, source.height);
            var actual = TextureDiagnostics.Render(Texture2D.blackTexture, material, 1, source.width, source.height);
            Assert.That(Bc7TestData.MaxError(expected, actual), Is.Zero);
        }

        [Test]
        public void WrongKeyDoesNotRenderTheOriginal()
        {
            var source = Own(TextureDiagnostics.Pattern(64, 32, true, false));
            Material material = Prepare(source, out _);
            material.SetFloat("_Key0", (TextureDiagnostics.Key[0] + 1) & 255);
            var reference = TextureDiagnostics.Render(source, material, 0, 64, 32);
            var actual = TextureDiagnostics.Render(Texture2D.blackTexture, material, 1, 64, 32);
            Assert.That(Bc7TestData.MaxError(reference, actual), Is.GreaterThan(32));
        }
    }
}
#endif
