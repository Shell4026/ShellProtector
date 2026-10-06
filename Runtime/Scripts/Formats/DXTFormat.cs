using UnityEngine;
using System;
using System.Threading.Tasks;

#if UNITY_EDITOR

namespace Shell.Protector
{
    public abstract class DXTFormat : BaseTextureFormat {
        protected byte[] GetArrayDXT(byte[] data, int texture_width, int texture_height, bool dxt5, int miplv) {
            int start = 0;
            int end = 0;

            for (int i = 0; i <= miplv; ++i) {
                start += end;
                int w = Math.Max(1, texture_width >> i);
                int h = Math.Max(1, texture_height >> i);
                int block_count = Math.Max(1, w / 4) * Math.Max(1, h / 4);
                int len = block_count * 8;
                if (dxt5) len *= 2;
                end = len;
            }

            var segment = new ArraySegment<byte>(data, start, end);
            return segment.ToArray();
        }

        protected Texture2D HandleCrunchedFormat(Texture2D texture, int mip_lv, bool isDXT5) {
            if (!texture.name.Contains("Crunched")) return texture;
            
            Debug.LogWarningFormat("{0} is the crunch compression format. There may be degradation in image quality.", texture.name);
            var format = isDXT5 ? TextureFormat.RGBA32 : TextureFormat.RGB24;
            var result = new Texture2D(texture.width, texture.height, format, mip_lv, true);
            
            for (int m = 0; m <= mip_lv; ++m) {
                if (m != 0 && m == mip_lv) break;
                result.SetPixels32(texture.GetPixels32(m), m);
                result.Apply();
            }
            
            result.Compress(isDXT5);
            return result;
        }

        // Moves the endpoint word of every DXT block (at `endpoint` within each `blockSize`-byte block) into its
        // Texture2 texel, encrypted. ChaCha: one keystream block covers 4x4 DXT blocks (16x16 texels), one word per
        // block, so the shader decrypts once for the whole area. XXTEA: two horizontally adjacent blocks per unit.
        protected void EncryptEndpoints(byte[] tex_data, Color32[] pixel, int blockSize, int endpoint, int blocksPerRow, int mip, byte[] key, IEncryptor algorithm) {
            int blockCount = tex_data.Length / blockSize;

            if (algorithm is Chacha20 chacha) {
                for (int b = 0; b < blockCount; ++b) {
                    int i = b * blockSize + endpoint;
                    pixel[b] = new Color32(tex_data[i + 0], tex_data[i + 1], tex_data[i + 2], tex_data[i + 3]);
                }
                EncryptBlocks(pixel, blocksPerRow, pixel.Length / blocksPerRow, mip, key, chacha, true);
                return;
            }

            // Units are independent and write disjoint texels, so they run in parallel with their own key array.
            Parallel.For(0, blockCount / 2, unit => {
                int b = unit * 2;
                var key_uint = ConvertKeyToUInt(key);
                key_uint[3] = GetUnitKey(key, (uint)b, mip);

                uint[] data = new uint[2];
                for (int j = 0; j < 2; ++j) {
                    int i = (b + j) * blockSize + endpoint;
                    data[j] = (uint)(tex_data[i + 0] + (tex_data[i + 1] << 8) + (tex_data[i + 2] << 16) + (tex_data[i + 3] << 24));
                }

                uint[] data_enc = algorithm.Encrypt(data, key_uint);

                for (int j = 0; j < 2; ++j) {
                    pixel[b + j].r = (byte)((data_enc[j] & 0x000000FF) >> 0);
                    pixel[b + j].g = (byte)((data_enc[j] & 0x0000FF00) >> 8);
                    pixel[b + j].b = (byte)((data_enc[j] & 0x00FF0000) >> 16);
                    pixel[b + j].a = (byte)((data_enc[j] & 0xFF000000) >> 24);
                }
            });
        }

        protected int GetDXTMipCount(Texture2D texture) {
            int count = 0;
            for (int mip = 0; mip < texture.mipmapCount; mip++) {
                int blocks = Math.Max(1, (texture.width >> mip) / 4) * Math.Max(1, (texture.height >> mip) / 4);
                if (blocks < 2) break;
                count++;
            }
            return Math.Max(1, count);
        }

        public override void SetFormatKeywords(Material material) {
            material.DisableKeyword(ShaderProperties.Format0Keyword);
            material.DisableKeyword(ShaderProperties.Format1Keyword);
        }

        public override (int, int) CalculateOffsets(Texture2D texture) {
            int woffset = 13 - (int)Mathf.Log(texture.width, 2) - 1 + 2;
            int hoffset = 13 - (int)Mathf.Log(texture.height, 2) - 1 + 2;
            return (woffset, hoffset);
        }
    }

    public class DXT1Format : DXTFormat {
        public override bool CanHandle(TextureFormat format) {
            return format == TextureFormat.DXT1 || format == TextureFormat.DXT1Crunched;
        }

        public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm) {
            if (texture.width < 8) {
                throw new Exception($"{texture.name} : The texture width must be >= 8px");
            }

            if (texture.height < 4) {
                throw new Exception($"{texture.name} : The texture height must be >= 4px");
            }

            int mip_lv = GetDXTMipCount(texture);
            Texture2D dxt1 = HandleCrunchedFormat(texture, mip_lv, false);
            
            var result = new EncryptResult();
            if (mip_lv != 0) {
                result.Texture1 = new Texture2D(dxt1.width, dxt1.height, TextureFormat.DXT1, mip_lv, true);
                result.Texture2 = new Texture2D(dxt1.width / 4, dxt1.height / 4, TextureFormat.RGBA32, mip_lv, true);
            } else {
                result.Texture1 = new Texture2D(dxt1.width, dxt1.height, TextureFormat.DXT1, false, true);
                result.Texture2 = new Texture2D(dxt1.width / 4, dxt1.height / 4, TextureFormat.RGBA32, false, true);
            }
            result.Texture2.filterMode = FilterMode.Point;
            result.Texture2.anisoLevel = 0;

            var raw_data = dxt1.GetRawTextureData();
            int lenidx = 0;

            for (int m = 0; m <= mip_lv; ++m) {
                if (m != 0 && m == mip_lv) break;
                var tex_data = GetArrayDXT(raw_data, dxt1.width, dxt1.height, false, m);
                var pixel = result.Texture2.GetPixels32(m);

                EncryptEndpoints(tex_data, pixel, 8, 0, Mathf.Max(1, result.Texture2.width >> m), m, key, algorithm);
                for (int i = 0; i < tex_data.Length; i += 8) {
                    tex_data[i + 0] = 255;
                    tex_data[i + 1] = 255;
                    tex_data[i + 2] = 0;
                    tex_data[i + 3] = 0;
                }
                for (int i = 0; i < tex_data.Length; ++i) {
                    raw_data[i + lenidx] = tex_data[i];
                }
                lenidx += tex_data.Length;
                result.Texture2.SetPixels32(pixel, m);
            }
            result.Texture1.LoadRawTextureData(raw_data);
            result.Texture1.filterMode = FilterMode.Point;
            result.Texture1.anisoLevel = 0;

            return result;
        }
    }

    public class DXT5Format : DXTFormat {
        public override bool CanHandle(TextureFormat format) {
            return format == TextureFormat.DXT5 || format == TextureFormat.DXT5Crunched;
        }

        public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm) {
            if (texture.width < 8) {
                throw new Exception($"{texture.name} : The texture width must be >= 8px");
            }

            if (texture.height < 4) {
                throw new Exception($"{texture.name} : The texture height must be >= 4px");
            }

            int mip_lv = GetDXTMipCount(texture);
            Texture2D dxt5 = HandleCrunchedFormat(texture, mip_lv, true);
            
            var result = new EncryptResult();
            if (mip_lv != 0) {
                result.Texture1 = new Texture2D(texture.width, texture.height, TextureFormat.DXT5, mip_lv, true);
                result.Texture2 = new Texture2D(texture.width / 4, texture.height / 4, TextureFormat.RGBA32, mip_lv, true);
            } else {
                result.Texture1 = new Texture2D(texture.width, texture.height, TextureFormat.DXT5, false, true);
                result.Texture2 = new Texture2D(texture.width / 4, texture.height / 4, TextureFormat.RGBA32, false, true);
            }
            result.Texture1.alphaIsTransparency = true;
            result.Texture2.filterMode = FilterMode.Point;
            result.Texture2.anisoLevel = 0;

            var raw_data = dxt5.GetRawTextureData();
            int lenidx = 0;

            for (int m = 0; m <= mip_lv; ++m) {
                if (m != 0 && m == mip_lv) break;
                var tex_data = GetArrayDXT(raw_data, texture.width, texture.height, true, m);
                var pixel = result.Texture2.GetPixels32(m);

                EncryptEndpoints(tex_data, pixel, 16, 8, Mathf.Max(1, result.Texture2.width >> m), m, key, algorithm);
                for (int i = 0; i < tex_data.Length; i += 16) {
                    tex_data[i + 8] = 255;
                    tex_data[i + 9] = 255;
                    tex_data[i + 10] = 0;
                    tex_data[i + 11] = 0;
                }
                for (int i = 0; i < tex_data.Length; ++i) {
                    raw_data[i + lenidx] = tex_data[i];
                }
                lenidx += tex_data.Length;
                result.Texture2.SetPixels32(pixel, m);
            }
            result.Texture1.LoadRawTextureData(raw_data);
            result.Texture1.filterMode = FilterMode.Point;
            result.Texture1.anisoLevel = 0;

            return result;
        }
    }
}

#endif
