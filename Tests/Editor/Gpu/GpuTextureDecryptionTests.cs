#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;

namespace Shell.Protector.Tests.Gpu
{
    public class GpuTextureDecryptionTests
    {
        private const int Size = 16;
        private static readonly byte[] KeyBytes = TestKeys.Default;
        private Material referenceMaterial;
        private Material decryptMaterial;
        private Texture2D mipTexture;

        [SetUp]
        public void SetUp()
        {
            Shader shader = Shader.Find("Hidden/GpuDecryptTest");
            Assert.That(shader, Is.Not.Null, "GPU decrypt test shader was not imported.");
            Assert.That(shader.isSupported, Is.True, "GPU decrypt test shader is not supported or failed to compile.");

            referenceMaterial = new Material(shader);
            decryptMaterial = new Material(shader);
            mipTexture = new Texture2D(1, 1, TextureFormat.RGB24, false, true);
            mipTexture.SetPixel(0, 0, Color.black);
            mipTexture.filterMode = FilterMode.Point;
            mipTexture.Apply(false, false);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(referenceMaterial);
            UnityEngine.Object.DestroyImmediate(decryptMaterial);
            UnityEngine.Object.DestroyImmediate(mipTexture);
        }

        [TestCase(TextureFormat.RGB24, false, false)]
        [TestCase(TextureFormat.RGB24, false, true)]
        [TestCase(TextureFormat.RGBA32, true, false)]
        [TestCase(TextureFormat.RGBA32, true, true)]
        [TestCase(TextureFormat.DXT1, false, false)]
        [TestCase(TextureFormat.DXT1, false, true)]
        [TestCase(TextureFormat.DXT5, true, false)]
        [TestCase(TextureFormat.DXT5, true, true)]
        public void XxteaEncryptedTexture_DecryptsToOriginalGpuSample(TextureFormat format, bool alpha, bool bilinear)
        {
            XXTEA xxtea = new XXTEA { Rounds = 20 };

            AssertDecryptsToOriginalGpuSample(format, alpha, bilinear, xxtea, material =>
            {
                material.SetInteger("_Rounds", 20);
            });
        }

        [TestCase(TextureFormat.RGB24, false, false)]
        [TestCase(TextureFormat.RGB24, false, true)]
        [TestCase(TextureFormat.RGBA32, true, false)]
        [TestCase(TextureFormat.RGBA32, true, true)]
        [TestCase(TextureFormat.DXT1, false, false)]
        [TestCase(TextureFormat.DXT1, false, true)]
        [TestCase(TextureFormat.DXT5, true, false)]
        [TestCase(TextureFormat.DXT5, true, true)]
        public void ChachaEncryptedTexture_DecryptsToOriginalGpuSample(TextureFormat format, bool alpha, bool bilinear)
        {
            Chacha20 chacha = CreateChacha();
            AssertDecryptsToOriginalGpuSample(format, alpha, bilinear, chacha, material => ConfigureChacha(material, chacha));
        }

        // Every mip level has its own key, so mip 1 has to decrypt too. Box only: bilinear steps by mip 0 texels.
        [TestCase(TextureFormat.RGB24, false)]
        [TestCase(TextureFormat.RGBA32, true)]
        [TestCase(TextureFormat.DXT1, false)]
        [TestCase(TextureFormat.DXT5, true)]
        public void ChachaEncryptedTexture_DecryptsMipLevel(TextureFormat format, bool alpha)
        {
            Chacha20 chacha = CreateChacha();
            AssertDecryptsToOriginalGpuSample(format, alpha, false, chacha, material => ConfigureChacha(material, chacha), 1);
        }

        private static Chacha20 CreateChacha()
        {
            Chacha20 chacha = new Chacha20();
            for (int i = 0; i < chacha.Nonce.Length; i++)
                chacha.Nonce[i] = (byte)i;
            return chacha;
        }

        private static void ConfigureChacha(Material material, Chacha20 chacha)
        {
            uint[] nonce = chacha.GetNonceUint3();
            material.SetInteger("_Nonce0", unchecked((int)nonce[0]));
            material.SetInteger("_Nonce1", unchecked((int)nonce[1]));
            material.SetInteger("_Nonce2", unchecked((int)nonce[2]));
        }

        private void AssertDecryptsToOriginalGpuSample(TextureFormat format, bool alpha, bool bilinear, IEncryptor encryptor, Action<Material> configureCipher, int mip = 0)
        {
            Texture2D original = TestAssetScope.CreatePatternTexture(Size, Size, format, alpha);
            original.filterMode = FilterMode.Point;
            original.wrapMode = TextureWrapMode.Repeat;

            EncryptResult encrypted = TextureEncryptManager.EncryptTexture(original, KeyBytes, encryptor);
            FinalizeTexture(encrypted.Texture1);
            FinalizeTexture(encrypted.Texture2);

            ConfigureReferenceMaterial(original, mip);
            ConfigureDecryptMaterial(original, encrypted, encryptor, configureCipher, mip);

            int size = Size >> mip;
            Color32[] reference = Render(referenceMaterial, original, mip == 0 ? 0 : 3, size, size);
            Color32[] decrypted = Render(decryptMaterial, Texture2D.blackTexture, bilinear ? 2 : 1, size, size);

            AssertPixelsEqual(reference, decrypted, format, encryptor.Keyword, bilinear);
        }

        private void ConfigureReferenceMaterial(Texture2D original, int mip)
        {
            referenceMaterial.SetTexture("_MainTex", original);
            referenceMaterial.SetFloat("_Lod", mip);
        }

        private void ConfigureDecryptMaterial(Texture2D original, EncryptResult encrypted, IEncryptor encryptor, Action<Material> configureCipher, int mip)
        {
            // The shader reads the mip level as round(r * 255 / 10).
            mipTexture.SetPixel(0, 0, new Color32((byte)(mip * 10), 0, 0, 255));
            mipTexture.Apply(false, false);

            decryptMaterial.shaderKeywords = Array.Empty<string>();
            decryptMaterial.mainTexture = original;
            decryptMaterial.SetTexture("_EncryptTex0", encrypted.Texture1);
            decryptMaterial.SetTexture("_EncryptTex1", encrypted.Texture2 != null ? encrypted.Texture2 : Texture2D.blackTexture);
            decryptMaterial.SetTexture("_MipTex", mipTexture);

            for (int i = 0; i < KeyBytes.Length; i++)
                decryptMaterial.SetFloat("_Key" + i, KeyBytes[i]);

            var offsets = TextureEncryptManager.CalculateOffsets(original);
            decryptMaterial.SetInteger("_Woffset", offsets.Item1);
            decryptMaterial.SetInteger("_Hoffset", offsets.Item2);
            decryptMaterial.SetInteger("_HashMagic", unchecked((int)0x12345678u));
            decryptMaterial.SetInteger("_PasswordHash", unchecked((int)KeyGenerator.SimpleHash(KeyBytes, 0x12345678u)));

            TextureEncryptManager.SetFormatKeywords(decryptMaterial);
            decryptMaterial.EnableKeyword(encryptor.Keyword);
            configureCipher(decryptMaterial);
        }

        private static void FinalizeTexture(Texture2D texture)
        {
            if (texture == null)
                return;

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply(false, false);
        }

        private static Color32[] Render(Material material, Texture source, int pass, int width, int height)
        {
            RenderTexture target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.filterMode = FilterMode.Point;
            target.Create();

            RenderTexture previous = RenderTexture.active;
            Texture2D readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(source, target, material, pass);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply(false, false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        private static void AssertPixelsEqual(Color32[] expected, Color32[] actual, TextureFormat format, string keyword, bool bilinear)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                if (!expected[i].Equals(actual[i]))
                {
                    Assert.Fail(
                        "{0} {1} {2} pixel {3} differs. Expected RGBA({4},{5},{6},{7}), Actual RGBA({8},{9},{10},{11})",
                        format,
                        keyword,
                        bilinear ? "bilinear" : "box",
                        i,
                        expected[i].r,
                        expected[i].g,
                        expected[i].b,
                        expected[i].a,
                        actual[i].r,
                        actual[i].g,
                        actual[i].b,
                        actual[i].a);
                }
            }
        }
    }
}
#endif
