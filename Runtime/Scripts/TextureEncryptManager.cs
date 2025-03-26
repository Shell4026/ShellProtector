using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR

namespace Shell.Protector
{
    public class TextureEncryptManager
    {
        private static readonly Dictionary<TextureFormat, ITextureFormat> _formats = new Dictionary<TextureFormat, ITextureFormat> {
            { TextureFormat.DXT1, new DXT1Format() },
            { TextureFormat.DXT1Crunched, new DXT1Format() },
            { TextureFormat.DXT5, new DXT5Format() },
            { TextureFormat.DXT5Crunched, new DXT5Format() },
            { TextureFormat.RGB24, new RGB24Format() },
            { TextureFormat.RGBA32, new RGBA32Format() }
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

        public static Texture2D GenerateRefMipmap(int width, int height, bool small = false)
        {
            int mip_lv = GetCanMipmapLevel(width, height);
            Debug.LogFormat("mip {0}, {1} : {2}", width, height, mip_lv);

            Texture2D mip = new Texture2D(width, (small == false) ? height : 1, TextureFormat.RGB24, mip_lv, true);
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

        private static int GetCanMipmapLevel(int w, int h)
        {
            if (w < 1 || h <= 1) return 0;
            int w_level = (int)Mathf.Log(w, 2);
            int h_level = (int)Mathf.Log(h, 2);
            return Mathf.Max(w_level, h_level);
        }

        private static ITextureFormat GetFormat(Texture texture)
        {
            if (texture == null)
            {
                return null;
            }

            if (texture is not Texture2D texture2D)
            {
                return null;
            }

            return _formats.FirstOrDefault(f => f.Value.CanHandle(texture2D.format)).Value;
        }

        private static ITextureFormat GetFormat(Material material)
        {
            if (material == null || material.mainTexture == null)
            {
                return null;
            }

            return GetFormat(material.mainTexture as Texture2D);
        }

        public static EncryptResult EncryptTexture(Texture2D texture, byte[] key, IEncryptor encryptor)
        {
            if (texture.width % 2 != 0 && texture.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", texture.name);
                return new EncryptResult();
            }

            var format = GetFormat(texture);
            if (format == null)
            {
                Debug.LogErrorFormat("{0} is not supported texture format! supported type:DXT1, DXT5, RGB, RGBA", texture.name);
                return new EncryptResult();
            }

            return format.Encrypt(texture, key, encryptor);
        }

        public static bool IsSupportedFormat(Material material)
        {
            return GetFormat(material) != null;
        }

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

        public static (int, int) CalculateOffsets(Texture2D texture)
        {
            var format = GetFormat(texture);
            if (format == null) return (0, 0);
            return format.CalculateOffsets(texture);
        }
    }
}

#endif