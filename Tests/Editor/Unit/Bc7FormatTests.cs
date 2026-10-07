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

        static Chacha20 Cipher()
        {
            var chacha = new Chacha20();
            for (int i = 0; i < chacha.Nonce.Length; ++i) chacha.Nonce[i] = (byte)(i * 37 + 11);
            return chacha;
        }

        EncryptResult Encrypt(Texture2D source, Chacha20 chacha)
        {
            var result = new BC7Format().Encrypt(source, Bc7TestData.Key, chacha);
            Own(result.Texture1);
            return result;
        }

        [Test]
        public void ProducesOnlyAnEncryptedAtlasSmallerThanRgbaWithTheSameMips()
        {
            var result = Encrypt(Constant(64, 32, true), Cipher());
            Assert.That(result.Texture1.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(result.Texture1.mipmapCount, Is.EqualTo(1), "The ciphertext atlas must not be mip-filtered.");
            Assert.That(result.Texture2, Is.Null, "No plaintext carrier is part of the output contract.");
            Assert.That(result.Texture1.GetRawTextureData().LongLength, Is.LessThan(result.Layout.RgbaBytes));
        }

        [Test]
        public void IdenticalRecordsAtDifferentMipsDoNotReuseCiphertext()
        {
            var result = Encrypt(Constant(16, 16, true), Cipher());
            byte[] bytes = result.Texture1.GetRawTextureData();
            byte[] first = bytes.Take(32).ToArray();
            Assert.That(bytes.Skip(32).Take(32).ToArray(), Is.Not.EqualTo(first), "Different block addresses.");
            byte[] nextMip = bytes.Skip(result.Layout.MipBlockOffsets[1] * 32).Take(32).ToArray();
            Assert.That(nextMip, Is.Not.EqualTo(first));
        }

        [TestCase(32, 16)]
        [TestCase(4, 4)] // Padding and the mip tail make this atlas larger than RGBA32; it is still encrypted.
        public void AllRecordBytesRoundTripThroughTheNonceAndMipBlockDomain(int width, int height)
        {
            var source = Constant(width, height, true);
            var cipher = Cipher();
            var result = Encrypt(source, cipher);
            byte[] bytes = result.Texture1.GetRawTextureData();
            Assert.That(bytes.LongLength, Is.EqualTo(result.Layout.AtlasBytes));
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
            var layout = new BC7TextureLayout(source, source.mipmapCount);
            var atlas = Own(new Texture2D(layout.AtlasWidth, layout.AtlasHeight, TextureFormat.RGBA32, false, true));
            Assert.That(atlas.GetRawTextureData().LongLength, Is.EqualTo(layout.AtlasBytes));
            Assert.That(layout.AtlasBytes, Is.LessThan(layout.RgbaBytes));
        }

        [Test]
        public void RejectsTexturesLargerThanFourK()
        {
            var source = Own(new Texture2D(8192, 4, TextureFormat.BC7, false, true));
            Assert.Throws<ArgumentException>(() => new BC7Format().Encrypt(source, Bc7TestData.Key, Cipher()));
        }

        [Test]
        public void RejectsXxteaWithAnActionableError()
        {
            var source = Constant(16, 16, false);
            var error = Assert.Throws<ArgumentException>(() => new BC7Format().Encrypt(source, Bc7TestData.Key, new XXTEA()));
            Assert.That(error.Message, Does.Contain("only be encrypted with ChaCha"));
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
