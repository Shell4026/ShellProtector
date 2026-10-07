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
            Own(result.Texture2);
            return result;
        }

        [Test]
        public void ProducesPlainCodesAndAnEncryptedEndpointAtlasSmallerThanRgba()
        {
            var source = Constant(64, 32, true);
            var result = Encrypt(source, Cipher());
            Assert.That(result.Texture1.format, Is.EqualTo(TextureFormat.R8));
            Assert.That(result.Texture1.mipmapCount, Is.EqualTo(source.mipmapCount), "The codes keep the source's mip chain.");
            Assert.That(result.Texture2.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(result.Texture2.mipmapCount, Is.EqualTo(1), "The ciphertext atlas must not be mip-filtered.");
            Assert.That(result.Texture2.GetRawTextureData().LongLength, Is.EqualTo(result.Layout.AtlasBytes));
            Assert.That(result.Texture1.GetRawTextureData().LongLength + result.Layout.AtlasBytes, Is.LessThan(result.Layout.RgbaBytes));
        }

        [Test]
        public void IdenticalRecordsAtDifferentMipsDoNotReuseCiphertext()
        {
            var result = Encrypt(Constant(16, 16, true), Cipher());
            byte[] bytes = result.Texture2.GetRawTextureData();
            byte[] first = bytes.Take(16).ToArray();
            Assert.That(bytes.Skip(16).Take(16).ToArray(), Is.Not.EqualTo(first), "Different blocks of one keystream unit.");
            Assert.That(bytes.Skip(32).Take(16).ToArray(), Is.Not.EqualTo(first), "Different keystream units.");
            byte[] nextMip = bytes.Skip(result.Layout.MipBlockOffsets[1] * 16).Take(16).ToArray();
            Assert.That(nextMip, Is.Not.EqualTo(first));
        }

        [TestCase(32, 16)]
        [TestCase(4, 4)] // Padding and the mip tail make this atlas larger than RGBA32; it is still encrypted.
        public void AllRecordBytesRoundTripThroughTheNonceAndMipBlockDomain(int width, int height)
        {
            var source = Constant(width, height, true);
            var cipher = Cipher();
            var result = Encrypt(source, cipher);
            byte[] bytes = result.Texture2.GetRawTextureData();
            Assert.That(bytes.LongLength, Is.EqualTo(result.Layout.AtlasBytes));
            byte[] expected = new byte[16], expectedCodes = new byte[16];
            BC7Codec.Normalize(source.GetPixelData<byte>(0).ToArray().AsSpan(0, 16), expected, expectedCodes);
            uint[] words = new uint[4], stream = new uint[16], key = new uint[4];
            Buffer.BlockCopy(Bc7TestData.Key, 0, key, 0, 16); uint last = key[3];
            for (int mip = 0; mip < result.Layout.MipCount; ++mip)
            {
                int w = Math.Max(1, width >> mip), h = Math.Max(1, height >> mip);
                int blockWidth = (w + 3) / 4, blockHeight = (h + 3) / 4, unitsPerRow = (blockWidth + 1) / 2;
                for (int by = 0; by < blockHeight; ++by)
                    for (int bx = 0; bx < blockWidth; ++bx)
                    {
                        // One keystream per 2x2 blocks; block (bx & 1) + 2 * (by & 1) of the unit uses words 4k to 4k + 3.
                        int unit = (by / 2) * unitsPerRow + bx / 2, local = (bx & 1) | ((by & 1) << 1);
                        Array.Clear(stream, 0, stream.Length);
                        key[3] = last ^ (uint)unit ^ ((uint)mip << 24); cipher.XorKeyStream(stream, key);
                        Buffer.BlockCopy(bytes, (result.Layout.MipBlockOffsets[mip] + by * blockWidth + bx) * 16, words, 0, 16);
                        for (int i = 0; i < 4; ++i) words[i] ^= stream[local * 4 + i];
                        byte[] decoded = new byte[16]; Buffer.BlockCopy(words, 0, decoded, 0, 16);
                        Assert.That(decoded, Is.EqualTo(expected), "Whole record at mip " + mip + " block " + bx + "," + by);
                    }
                byte[] codes = result.Texture1.GetPixelData<byte>(mip).ToArray();
                Assert.That(codes.Length, Is.EqualTo(w * h));
                for (int y = 0; y < h; ++y)
                    for (int x = 0; x < w; ++x)
                        Assert.That(codes[y * w + x], Is.EqualTo(expectedCodes[(y & 3) * 4 + (x & 3)]), "Code at mip " + mip + " pixel " + x + "," + y);
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
            Assert.Throws<ArgumentException>(() => BC7Codec.Normalize(new byte[15], new byte[16], new byte[16]));
            Assert.Throws<ArgumentException>(() => BC7Codec.Normalize(new byte[16], new byte[15], new byte[16]));
            Assert.Throws<ArgumentException>(() => BC7Codec.Normalize(new byte[16], new byte[16], new byte[15]));
        }
    }
}
#endif
