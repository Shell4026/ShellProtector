using UnityEngine;
using System;
using System.Linq;
using System.Security.Cryptography;

#if UNITY_EDITOR

namespace Shell.Protector
{
    // Thrown before any output is written when a selected texture cannot be encrypted.
    public sealed class EncryptionBlockedException : InvalidOperationException
    {
        public EncryptionBlockedException(string message) : base(message) { }
    }

    public class TextureEncryptManager
    {
        // The shader mip tables and the BC7 layout stop at 4096 pixels per side.
        public const int MaxTextureSize = 4096;

        private static readonly BaseTextureFormat[] _formats = {
            new DXT1Format(), new DXT5Format(), new RGB24Format(), new RGBA32Format(), new BC7Format()
        };

        public static bool HasAlpha(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a != 255) return true;
            }
            return false;
        }

        public static Texture2D GenerateRefMipmap(int width, int height, bool small = false, bool fullChain = false)
        {
            int mip_lv = fullChain ? TextureMipUtility.FullCount(width, height) : TextureMipUtility.LegacyLevel(width, height);
            Debug.LogFormat("mip {0}, {1} : {2}", width, height, mip_lv);

            // A one-row reference still needs the longest axis to hold every mip of a tall rectangle.
            int referenceWidth = small ? Mathf.Max(width, height) : width;
            Texture2D mip = new Texture2D(referenceWidth, small ? 1 : height, TextureFormat.RGB24, mip_lv, true);
            mip.filterMode = FilterMode.Bilinear;
            mip.anisoLevel = (small == false) ? 1 : 0;

            for (int m = 0; m < mip.mipmapCount; ++m)
            {
                Color32[] pixels_mip = mip.GetPixels32(m);
                for (int i = 0; i < pixels_mip.Length; ++i)
                {
                    pixels_mip[i].r = (byte)(m * 10);
                    pixels_mip[i].g = 0;
                    pixels_mip[i].b = 0;
                }
                mip.SetPixels32(pixels_mip, m);
            }
            if (small == false)
                mip.Compress(false);
            // SetPixels32/Compress update the CPU copy. New and reused GPU allocations must
            // receive these mip labels before a generated material samples the reference.
            mip.Apply(false, false);
            return mip;
        }

        public static Texture2D GenerateFallback(Texture2D original, int size = 32)
        {
            if (original.width < 128 || original.height < 128)
            {
                return null;
            }
            TextureFormat format = TextureFormat.RGB24;
            bool hasAlpha = HasAlpha(original);
            if (hasAlpha)
                format = TextureFormat.RGBA32;

            RenderTexture renderTexture = new RenderTexture(size, size, 0);
            RenderTexture.active = renderTexture;

            Graphics.Blit(original, renderTexture);

            Texture2D resizedTexture = new Texture2D(size, size, format, true);
            resizedTexture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            resizedTexture.Apply();

            resizedTexture.filterMode = FilterMode.Point;
            resizedTexture.anisoLevel = 0;
            resizedTexture.Compress(false);
            if (hasAlpha)
                resizedTexture.alphaIsTransparency = true;

            RenderTexture.active = null;

            return resizedTexture;
        }

        private static BaseTextureFormat GetFormat(Texture texture)
        {
            if (texture == null)
            {
                return null;
            }

            if (texture is not Texture2D texture2D)
            {
                return null;
            }

            return _formats.FirstOrDefault(f => f.CanHandle(texture2D.format));
        }

        private static BaseTextureFormat GetFormat(Material material)
        {
            if (material == null || material.mainTexture == null)
            {
                return null;
            }

            return GetFormat(material.mainTexture as Texture2D);
        }

        public static EncryptResult EncryptTexture(Texture2D texture, byte[] key, IEncryptor encryptor)
        {
            if (texture.width % 2 != 0 || texture.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", texture.name);
                return new EncryptResult();
            }

            var format = GetFormat(texture);
            if (format == null)
            {
                Debug.LogErrorFormat("{0} is not supported texture format! supported type:DXT1, DXT5, RGB, RGBA, BC7", texture.name);
                return new EncryptResult();
            }

            return format.Encrypt(texture, key, encryptor);
        }

        // Each encrypted texture gets its own cipher, so encrypting one texture never changes the
        // nonce another result was encrypted with. BC7 can only be decoded with ChaCha8.
        public static IEncryptor CreateCipher(Texture2D texture, ShellProtectorAlgorithm algorithm, uint rounds)
        {
            bool requiresChacha = GetFormat(texture)?.RequiresChacha ?? false;
            if (algorithm != ShellProtectorAlgorithm.Chacha && !requiresChacha)
                return new XXTEA { Rounds = rounds };
            if (algorithm != ShellProtectorAlgorithm.Chacha)
                Debug.LogWarningFormat("{0} : BC7 textures are always encrypted with ChaCha8, even when XXTEA is selected.", texture.name);

            var chacha = new Chacha20();
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(chacha.Nonce);
            return chacha;
        }

        public static bool IsSupportedFormat(Material material)
        {
            return GetFormat(material) != null;
        }

        public static void ConfigureMaterial(Material material, Texture2D original, EncryptResult encrypted)
        {
            var format = GetFormat(original);
            if (format != null) format.ConfigureMaterial(material, original, encrypted);
            else
            {
                material.SetInteger(ShaderProperties.WidthOffset, 0);
                material.SetInteger(ShaderProperties.HeightOffset, 0);
            }
        }

        // Returns why the texture would stop the build, or null when it can be encrypted.
        public static string GetBlockingIssue(Texture2D texture)
        {
            if (texture == null || (texture.width <= MaxTextureSize && texture.height <= MaxTextureSize))
                return null;
            return $"{texture.name} ({texture.width}x{texture.height}) is larger than {MaxTextureSize}px. Lower its Max Size to {MaxTextureSize} or less.";
        }

        internal static (int width, int height, bool fullChain) MipReference(Texture2D texture) => GetFormat(texture).MipReference(texture);

        internal static int FallbackSize(Texture2D texture, int requestedSize) => GetFormat(texture).FallbackSize(texture, requestedSize);

        public static bool IsSupportedTexture(Texture texture)
        {
            return GetFormat(texture) != null;
        }

        public static bool IsDXTFormat(TextureFormat format)
        {
            return format == TextureFormat.DXT1 || format == TextureFormat.DXT5;
        }

        public static void SetFormatKeywords(Material material)
        {
            var format = GetFormat(material);
            if (format == null) return;
            format.SetFormatKeywords(material);
        }

        public static void SetFormatKeywords(Material material, Texture texture)
        {
            var format = GetFormat(texture);
            if (format == null) return;
            format.SetFormatKeywords(material);
        }

        public static (int, int) CalculateOffsets(Texture2D texture)
        {
            var format = GetFormat(texture);
            if (format == null) return (0, 0);
            return format.CalculateOffsets(texture);
        }
    }
}

#endif
