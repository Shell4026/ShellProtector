#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shell.Protector.Tests.Unit
{
    public class Bc7FormatTests
    {
        readonly List<Object> objects = new List<Object>();
        T Own<T>(T obj) where T : Object { objects.Add(obj); return obj; }
        [TearDown] public void TearDown() { foreach (var obj in objects) Object.DestroyImmediate(obj); objects.Clear(); }

        Texture2D Constant(int width, int height, bool mips)
        {
            var texture = Own(new Texture2D(width, height, TextureFormat.BC7, mips, true));
            byte[] data = texture.GetRawTextureData();
            for (int b = 0; b < data.Length; b += 16) data[b] = 0x40;
            texture.LoadRawTextureData(data); texture.Apply(false, false);
            return texture;
        }

        [Test]
        public void ProducesOnlyAnEncryptedAtlasSmallerThanRgbaWithTheSameMips()
        {
            var source = Constant(64, 32, true);
            var result = TextureEncryptManager.EncryptTexture(source, Bc7TestData.Key, new Chacha20()); Own(result.Texture1);
            Assert.That(result.Texture1.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(result.Texture1.mipmapCount, Is.EqualTo(1), "The ciphertext atlas must not be mip-filtered.");
            Assert.That(result.Texture2, Is.Null, "No plaintext carrier is part of the output contract.");
            Assert.That(result.Texture1.GetRawTextureData().LongLength, Is.LessThan(result.Layout.RgbaBytes));
        }

        [Test]
        public void IndependentEncryptionUsesIndependentNonces()
        {
            var source = Constant(16, 16, true); var cipher = new Chacha20();
            var first = TextureEncryptManager.EncryptTexture(source, Bc7TestData.Key, cipher); Own(first.Texture1);
            byte[] nonce = (byte[])cipher.Nonce.Clone();
            var second = TextureEncryptManager.EncryptTexture(source, Bc7TestData.Key, cipher); Own(second.Texture1);
            Assert.That(cipher.Nonce, Is.Not.EqualTo(nonce));
            Assert.That(second.Texture1.GetRawTextureData(), Is.Not.EqualTo(first.Texture1.GetRawTextureData()));
        }

        [Test]
        public void IdenticalRecordsAtDifferentMipsDoNotReuseCiphertext()
        {
            var source = Constant(16, 16, true);
            var result = TextureEncryptManager.EncryptTexture(source, Bc7TestData.Key, new Chacha20()); Own(result.Texture1);
            byte[] bytes = result.Texture1.GetRawTextureData();
            byte[] first = bytes.Take(32).ToArray();
            Assert.That(bytes.Skip(32).Take(32).ToArray(), Is.Not.EqualTo(first), "Different block addresses.");
            byte[] nextMip = bytes.Skip(result.Layout.MipBlockOffsets[1] * 32).Take(32).ToArray();
            Assert.That(nextMip, Is.Not.EqualTo(first));
        }

        [Test]
        public void AllRecordBytesRoundTripThroughTheStoredNonceAndMipBlockDomain()
        {
            var source = Constant(32, 16, true); var cipher = new Chacha20();
            var result = TextureEncryptManager.EncryptTexture(source, Bc7TestData.Key, cipher); Own(result.Texture1);
            byte[] bytes = result.Texture1.GetRawTextureData();
            byte[] expected = new byte[32]; BC7Codec.Normalize(source.GetPixelData<byte>(0).ToArray().AsSpan(0, 16), expected);
            uint[] words = new uint[8], key = new uint[4];
            Buffer.BlockCopy(Bc7TestData.Key, 0, key, 0, 16); uint last = key[3];
            for (int mip = 0; mip < result.Layout.MipCount; ++mip)
            {
                int begin = result.Layout.MipBlockOffsets[mip];
                int end = mip + 1 < result.Layout.MipCount ? result.Layout.MipBlockOffsets[mip + 1] : result.Layout.BlockCount;
                for (int block = 0; block < end - begin; ++block)
                {
                    Buffer.BlockCopy(bytes, (begin + block) * 32, words, 0, 32);
                    key[3] = last ^ (uint)block ^ ((uint)mip << 24); cipher.XorKeyStream(words, key);
                    byte[] decoded = new byte[32]; Buffer.BlockCopy(words, 0, decoded, 0, 32);
                    Assert.That(decoded, Is.EqualTo(expected), "Whole record at mip " + mip + " block " + block);
                }
            }
        }

        [TestCase(4096, 4096)] [TestCase(4096, 2048)] [TestCase(4, 4096)]
        public void AtlasAllocationIncludesPaddingAndFitsTheFourKRange(int width, int height)
        {
            var source = Own(new Texture2D(width, height, TextureFormat.BC7, true, true));
            var layout = new EncryptedTextureLayout(source, source.mipmapCount);
            var atlas = Own(new Texture2D(layout.AtlasWidth, layout.AtlasHeight, TextureFormat.RGBA32, false, true));
            Assert.That(atlas.GetRawTextureData().LongLength, Is.EqualTo(layout.AtlasBytes));
            Assert.That(layout.AtlasBytes, Is.LessThan(layout.RgbaBytes));
        }

        [Test]
        public void SmallMipReferenceSupportsTheFullChainOfATallRectangle()
        {
            var reference = Own(TextureEncryptManager.GenerateRefMipmap(4, 4096, true, true));
            Assert.That(reference.mipmapCount, Is.EqualTo(13));
            Assert.That(reference.GetPixels32(12)[0].r, Is.EqualTo(120));
        }

        [Test]
        public void RejectsXxteaForBc7WithAnActionableError()
        {
            var source = Constant(16, 16, false);
            var error = Assert.Throws<ArgumentException>(() => TextureEncryptManager.EncryptTexture(source, Bc7TestData.Key, new XXTEA()));
            Assert.That(error.Message, Does.Contain("requires ChaCha8"));
        }

        [TestCase(4, 4)]
        public void RejectsTextureWhenPaddingAndMipTailLoseTheMemorySaving(int width, int height)
        {
            var source = Constant(width, height, true);
            var error = Assert.Throws<ArgumentException>(() => TextureEncryptManager.EncryptTexture(source, Bc7TestData.Key, new Chacha20()));
            Assert.That(error.Message, Does.Contain("memory-saving requirement"));
        }

        [Test]
        public void RejectsWrongBlockLengths()
        {
            Assert.Throws<ArgumentException>(() => BC7Codec.Normalize(new byte[15], new byte[32]));
            Assert.Throws<ArgumentException>(() => BC7Codec.Normalize(new byte[16], new byte[31]));
        }
    }
}
#endif
