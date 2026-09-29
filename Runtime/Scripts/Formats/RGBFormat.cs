using UnityEngine;

#if UNITY_EDITOR

namespace Shell.Protector
{
    public abstract class RGBFormat : BaseTextureFormat {
        protected Texture2D CreateResultTexture(Texture2D texture, TextureFormat format) {
            int mip_lv = GetCanMipmapLevel(texture.width, texture.height);
            var result = new Texture2D(texture.width, texture.height, format, mip_lv - 2, true);
            result.filterMode = FilterMode.Point;
            result.anisoLevel = 0;
            return result;
        }

        // ChaCha is a stream cipher, so one 64-byte keystream block can cover a whole 4x4 pixel block
        // (one keystream word per pixel). The shader then derives a single keystream for every bilinear tap
        // inside the block instead of one per pixel. XXTEA keeps the per-pixel layout: sharing a key there
        // saves nothing because each chunk still needs its own decryption.
        protected static bool UseBlockStream(IEncryptor algorithm) {
            return algorithm is Chacha20;
        }

        protected void EncryptBlocks(Color32[] pixels, int width, int height, int mip, byte[] key, IEncryptor algorithm, bool alpha) {
            var key_uint = ConvertKeyToUInt(key);
            int blocksPerRow = (width + 3) / 4;
            int blockRows = (height + 3) / 4;
            uint alphaMask = alpha ? 0xFFFFFFFFu : 0x00FFFFFFu;
            uint[] data = new uint[16];

            for (int by = 0; by < blockRows; ++by) {
                for (int bx = 0; bx < blocksPerRow; ++bx) {
                    key_uint[3] = GetUnitKey(key, (uint)(by * blocksPerRow + bx), mip);

                    for (int j = 0; j < 16; ++j) {
                        int x = bx * 4 + (j & 3);
                        int y = by * 4 + (j >> 2);
                        if (x >= width || y >= height) {
                            data[j] = 0;
                            continue;
                        }
                        Color32 p = pixels[y * width + x];
                        data[j] = (uint)(p.r | (p.g << 8) | (p.b << 16) | (p.a << 24)) & alphaMask;
                    }

                    uint[] data_enc = algorithm.Encrypt(data, key_uint);

                    for (int j = 0; j < 16; ++j) {
                        int x = bx * 4 + (j & 3);
                        int y = by * 4 + (j >> 2);
                        if (x >= width || y >= height)
                            continue;
                        int i = y * width + x;
                        pixels[i].r = (byte)((data_enc[j] & 0x000000FF) >> 0);
                        pixels[i].g = (byte)((data_enc[j] & 0x0000FF00) >> 8);
                        pixels[i].b = (byte)((data_enc[j] & 0x00FF0000) >> 16);
                        if (alpha)
                            pixels[i].a = (byte)((data_enc[j] & 0xFF000000) >> 24);
                    }
                }
            }
        }
    }

    public class RGB24Format : RGBFormat {
        public override bool CanHandle(TextureFormat format) {
            return format == TextureFormat.RGB24;
        }

        public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm) {
            var result = new EncryptResult();
            result.Texture1 = CreateResultTexture(texture, TextureFormat.RGB24);

            var key_uint = ConvertKeyToUInt(key);

            for (int m = 0; m < result.Texture1.mipmapCount; ++m) {
                Color32[] pixels = texture.GetPixels32(m);

                if (UseBlockStream(algorithm)) {
                    EncryptBlocks(pixels, Mathf.Max(1, texture.width >> m), Mathf.Max(1, texture.height >> m), m, key, algorithm, false);
                    result.Texture1.SetPixels32(pixels, m);
                    continue;
                }

                for (int i = 0; i < pixels.Length; i += 4) {
                    key_uint[3] = GetUnitKey(key, (uint)i, m);

                    uint[] data = new uint[3];
                    data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 1].r << 24));
                    data[1] = (uint)(pixels[i + 1].g + (pixels[i + 1].b << 8) + (pixels[i + 2].r << 16) + (pixels[i + 2].g << 24));
                    data[2] = (uint)(pixels[i + 2].b + (pixels[i + 3].r << 8) + (pixels[i + 3].g << 16) + (pixels[i + 3].b << 24));

                    uint[] data_enc = algorithm.Encrypt(data, key_uint);

                    pixels[i + 0].r = (byte)((data_enc[0] & 0x000000FF) >> 0);
                    pixels[i + 0].g = (byte)((data_enc[0] & 0x0000FF00) >> 8);
                    pixels[i + 0].b = (byte)((data_enc[0] & 0x00FF0000) >> 16);
                    pixels[i + 1].r = (byte)((data_enc[0] & 0xFF000000) >> 24);
                    pixels[i + 1].g = (byte)((data_enc[1] & 0x000000FF) >> 0);
                    pixels[i + 1].b = (byte)((data_enc[1] & 0x0000FF00) >> 8);
                    pixels[i + 2].r = (byte)((data_enc[1] & 0x00FF0000) >> 16);
                    pixels[i + 2].g = (byte)((data_enc[1] & 0xFF000000) >> 24);
                    pixels[i + 2].b = (byte)((data_enc[2] & 0x000000FF) >> 0);
                    pixels[i + 3].r = (byte)((data_enc[2] & 0x0000FF00) >> 8);
                    pixels[i + 3].g = (byte)((data_enc[2] & 0x00FF0000) >> 16);
                    pixels[i + 3].b = (byte)((data_enc[2] & 0xFF000000) >> 24);
                }
                result.Texture1.SetPixels32(pixels, m);
            }

            return result;
        }

        public override void SetFormatKeywords(Material material) {
            material.EnableKeyword(ShaderProperties.Format0Keyword);
            material.DisableKeyword(ShaderProperties.Format1Keyword);
        }

        public override (int, int) CalculateOffsets(Texture2D texture) {
            int woffset = 13 - (int)Mathf.Log(texture.width, 2) - 1;
            int hoffset = 13 - (int)Mathf.Log(texture.height, 2) - 1;
            return (woffset, hoffset);
        }
    }

    public class RGBA32Format : RGBFormat {
        public override bool CanHandle(TextureFormat format) {
            return format == TextureFormat.RGBA32;
        }

        public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm) {
            var result = new EncryptResult();
            result.Texture1 = CreateResultTexture(texture, TextureFormat.RGBA32);

            var key_uint = ConvertKeyToUInt(key);

            for (int m = 0; m < result.Texture1.mipmapCount; ++m) {
                Color32[] pixels = texture.GetPixels32(m);

                if (UseBlockStream(algorithm)) {
                    EncryptBlocks(pixels, Mathf.Max(1, texture.width >> m), Mathf.Max(1, texture.height >> m), m, key, algorithm, true);
                    result.Texture1.SetPixels32(pixels, m);
                    continue;
                }

                for (int i = 0; i < pixels.Length; i += 2) {
                    key_uint[3] = GetUnitKey(key, (uint)i, m);

                    uint[] data = new uint[2];
                    data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 0].a << 24));
                    data[1] = (uint)(pixels[i + 1].r + (pixels[i + 1].g << 8) + (pixels[i + 1].b << 16) + (pixels[i + 1].a << 24));

                    uint[] data_enc = algorithm.Encrypt(data, key_uint);

                    pixels[i + 0].r = (byte)((data_enc[0] & 0x000000FF) >> 0);
                    pixels[i + 0].g = (byte)((data_enc[0] & 0x0000FF00) >> 8);
                    pixels[i + 0].b = (byte)((data_enc[0] & 0x00FF0000) >> 16);
                    pixels[i + 0].a = (byte)((data_enc[0] & 0xFF000000) >> 24);
                    pixels[i + 1].r = (byte)((data_enc[1] & 0x000000FF) >> 0);
                    pixels[i + 1].g = (byte)((data_enc[1] & 0x0000FF00) >> 8);
                    pixels[i + 1].b = (byte)((data_enc[1] & 0x00FF0000) >> 16);
                    pixels[i + 1].a = (byte)((data_enc[1] & 0xFF000000) >> 24);
                }
                result.Texture1.SetPixels32(pixels, m);
            }

            return result;
        }

        public override void SetFormatKeywords(Material material) {
            material.DisableKeyword(ShaderProperties.Format0Keyword);
            material.EnableKeyword(ShaderProperties.Format1Keyword);
        }

        public override (int, int) CalculateOffsets(Texture2D texture) {
            int woffset = 13 - (int)Mathf.Log(texture.width, 2) - 1;
            int hoffset = 13 - (int)Mathf.Log(texture.height, 2) - 1;
            return (woffset, hoffset);
        }
    }
}

#endif
