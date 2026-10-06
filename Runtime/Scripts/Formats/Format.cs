using System;
using System.Threading.Tasks;
using Shell.Protector;
using UnityEngine;

namespace Shell.Protector
{
    public struct EncryptResult {
        public Texture2D Texture1;
        public Texture2D Texture2;
    }

    public interface ITextureFormat {
        bool CanHandle(TextureFormat format);
        EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm);
        void SetFormatKeywords(Material material);
        (int, int) CalculateOffsets(Texture2D texture);
    }

    public abstract class BaseTextureFormat : ITextureFormat {
        protected uint[] ConvertKeyToUInt(byte[] key) {
            uint[] key_uint = new uint[4];
            key_uint[0] = (uint)(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
            key_uint[1] = (uint)(key[4] | (key[5] << 8) | (key[6] << 16) | (key[7] << 24));
            key_uint[2] = (uint)(key[8] | (key[9] << 8) | (key[10] << 16) | (key[11] << 24));
            key_uint[3] = 0;
            return key_uint;
        }

        // Last key word for one encryption unit. The mip level goes in the top 8 bits (unit indices stay below 2^24),
        // otherwise the same index on every mip level would reuse one ChaCha keystream.
        protected static uint GetUnitKey(byte[] key, uint idx, int mip) {
            return (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24)) ^ idx ^ ((uint)mip << 24);
        }

        protected int GetCanMipmapLevel(int w, int h) {
            if (w < 1 || h <= 1) return 0;
            int w_level = (int)Mathf.Log(w, 2);
            int h_level = (int)Mathf.Log(h, 2);
            return Mathf.Max(w_level, h_level);
        }

        // ChaCha is a stream cipher, so one 64-byte keystream block can cover a whole 4x4 pixel block
        // (one keystream word per pixel). The shader then derives a single keystream for every bilinear tap
        // inside the block instead of one per pixel. XXTEA keeps the per-pixel layout: sharing a key there
        // saves nothing because each chunk still needs its own decryption.
        // DXT uses the same layout on its endpoint texture, where one texel is one DXT block.
        protected void EncryptBlocks(Color32[] pixels, int width, int height, int mip, byte[] key, Chacha20 chacha, bool alpha) {
            int blocksPerRow = (width + 3) / 4;
            int blockRows = (height + 3) / 4;
            uint alphaMask = alpha ? 0xFFFFFFFFu : 0x00FFFFFFu;

            // Blocks are independent and write disjoint pixels, so rows of blocks run in parallel.
            Parallel.For(0, blockRows, by => {
                var key_uint = ConvertKeyToUInt(key);
                Span<uint> data = stackalloc uint[16];

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

                    chacha.XorKeyStream(data, key_uint);

                    for (int j = 0; j < 16; ++j) {
                        int x = bx * 4 + (j & 3);
                        int y = by * 4 + (j >> 2);
                        if (x >= width || y >= height)
                            continue;
                        int i = y * width + x;
                        pixels[i].r = (byte)((data[j] & 0x000000FF) >> 0);
                        pixels[i].g = (byte)((data[j] & 0x0000FF00) >> 8);
                        pixels[i].b = (byte)((data[j] & 0x00FF0000) >> 16);
                        if (alpha)
                            pixels[i].a = (byte)((data[j] & 0xFF000000) >> 24);
                    }
                }
            });
        }

        public abstract bool CanHandle(TextureFormat format);
        public abstract EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm);
        public abstract void SetFormatKeywords(Material material);
        public abstract (int, int) CalculateOffsets(Texture2D texture);
    }
}
