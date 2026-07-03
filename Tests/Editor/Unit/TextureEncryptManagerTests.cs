#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Shell.Protector.Tests.Unit
{
    public class TextureEncryptManagerTests
    {
        [TestCase(TextureFormat.RGB24, true)]
        [TestCase(TextureFormat.RGBA32, true)]
        [TestCase(TextureFormat.DXT1, false)]
        [TestCase(TextureFormat.DXT5, true)]
        public void SupportsExpectedTextureFormats(TextureFormat format, bool alpha)
        {
            Texture2D texture = TestAssetScope.CreatePatternTexture(16, 16, format, alpha);

            Assert.That(TextureEncryptManager.IsSupportedTexture(texture), Is.True);
        }

        [Test]
        public void CalculatesOffsetsPerFormat()
        {
            Texture2D rgb = TestAssetScope.CreatePatternTexture(16, 16, TextureFormat.RGB24, false);
            Texture2D rgba = TestAssetScope.CreatePatternTexture(16, 16, TextureFormat.RGBA32, true);
            Texture2D dxt = TestAssetScope.CreatePatternTexture(16, 16, TextureFormat.DXT1, false);

            Assert.That(TextureEncryptManager.CalculateOffsets(rgb), Is.EqualTo((8, 8)));
            Assert.That(TextureEncryptManager.CalculateOffsets(rgba), Is.EqualTo((8, 8)));
            Assert.That(TextureEncryptManager.CalculateOffsets(dxt), Is.EqualTo((10, 10)));
        }

        [Test]
        public void GeneratesMipReferenceTexture()
        {
            Texture2D mip = TextureEncryptManager.GenerateRefMipmap(16, 16, false);

            Assert.That(mip.width, Is.EqualTo(16));
            Assert.That(mip.height, Is.EqualTo(16));
            Assert.That(mip.GetPixels32(0)[0].r, Is.EqualTo(0));
            Assert.That(mip.GetPixels32(1)[0].r, Is.EqualTo(10));
        }

        [Test]
        public void GeneratesFallbackTexture()
        {
            Texture2D original = TestAssetScope.CreatePatternTexture(128, 128, TextureFormat.RGBA32, true);

            Texture2D fallback = TextureEncryptManager.GenerateFallback(original, 32);

            Assert.That(fallback, Is.Not.Null);
            Assert.That(fallback.width, Is.EqualTo(32));
            Assert.That(fallback.height, Is.EqualTo(32));
            Assert.That(fallback.format, Is.EqualTo(TextureFormat.DXT5));
            Assert.That(fallback.filterMode, Is.EqualTo(FilterMode.Point));
        }

        [TestCase(TextureFormat.RGB24, true, TextureFormat.RGB24)]
        [TestCase(TextureFormat.RGBA32, true, TextureFormat.RGBA32)]
        [TestCase(TextureFormat.DXT1, false, TextureFormat.DXT1)]
        [TestCase(TextureFormat.DXT5, true, TextureFormat.DXT5)]
        public void EncryptTextureCreatesExpectedResultTextures(TextureFormat sourceFormat, bool alpha, TextureFormat encryptedFormat)
        {
            Texture2D texture = TestAssetScope.CreatePatternTexture(16, 16, sourceFormat, alpha);
            XXTEA xxtea = new XXTEA { Rounds = 20 };
            byte[] key = KeyGenerator.MakeKeyBytes("password", "pass", 12);

            EncryptResult result = TextureEncryptManager.EncryptTexture(texture, key, xxtea);

            Assert.That(result.Texture1, Is.Not.Null);
            Assert.That(result.Texture1.format, Is.EqualTo(encryptedFormat));
            if (TextureEncryptManager.IsDXTFormat(sourceFormat))
            {
                Assert.That(result.Texture2, Is.Not.Null);
                Assert.That(result.Texture2.format, Is.EqualTo(TextureFormat.RGBA32));
            }
            else
            {
                Assert.That(result.Texture2, Is.Null);
            }
        }
    }
}
#endif
