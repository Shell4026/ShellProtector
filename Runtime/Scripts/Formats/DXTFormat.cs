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
                int w = texture_width / (int)(Mathf.Pow(2, i));
                int h = texture_height / (int)(Mathf.Pow(2, i));
                int block_count = (w / 4) * (h / 4);
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

        public override void SetFormatKeywords(Material material) {
            material.DisableKeyword(ShaderProperties.Format0Keyword);
            material.DisableKeyword(ShaderProperties.Format1Keyword);
        }

        public override (int, int) CalculateOffsets(Texture2D texture) => TextureMipUtility.Offsets(texture, 2);
        protected abstract bool HasAlpha { get; }

        public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm)
        {
            if (texture.width < 8) throw new Exception($"{texture.name} : The texture width must be >= 8px");
            if (texture.height < 4) throw new Exception($"{texture.name} : The texture height must be >= 4px");
            int mipLevel = TextureMipUtility.LegacyLevel(texture.width / 4, texture.height / 4);
            Texture2D input = HandleCrunchedFormat(texture, mipLevel, HasAlpha);
            TextureFormat format = HasAlpha ? TextureFormat.DXT5 : TextureFormat.DXT1;
            var result = new EncryptResult(algorithm);
            if (mipLevel != 0) {
                result.Texture1 = new Texture2D(input.width, input.height, format, mipLevel, true);
                result.Texture2 = new Texture2D(input.width / 4, input.height / 4, TextureFormat.RGBA32, mipLevel, true);
            } else {
                result.Texture1 = new Texture2D(input.width, input.height, format, false, true);
                result.Texture2 = new Texture2D(input.width / 4, input.height / 4, TextureFormat.RGBA32, false, true);
            }
            if (HasAlpha) result.Texture1.alphaIsTransparency = true;
            result.Texture2.filterMode = FilterMode.Point;
            result.Texture2.anisoLevel = 0;
            byte[] raw = input.GetRawTextureData();
            int destination = 0, blockBytes = HasAlpha ? 16 : 8, colorOffset = HasAlpha ? 8 : 0;
            for (int m = 0; m <= mipLevel; ++m)
            {
                if (m != 0 && m == mipLevel) break;
                byte[] blocks = GetArrayDXT(raw, input.width, input.height, HasAlpha, m);
                Color32[] pixels = result.Texture2.GetPixels32(m);
                Parallel.For(0, blocks.Length / (blockBytes * 2), unit => {
                    int block = unit * 2;
                    uint[] unitKey = ConvertKeyToUInt(key);
                    unitKey[3] = GetUnitKey(key, (uint)block, m);
                    uint[] endpoints = new uint[2];
                    for (int j = 0; j < 2; ++j) {
                        int address = (block + j) * blockBytes + colorOffset;
                        endpoints[j] = (uint)(blocks[address] | (blocks[address + 1] << 8) | (blocks[address + 2] << 16) | (blocks[address + 3] << 24));
                    }
                    uint[] encrypted = algorithm.Encrypt(endpoints, unitKey);
                    for (int j = 0; j < 2; ++j)
                        pixels[block + j] = new Color32((byte)encrypted[j], (byte)(encrypted[j] >> 8), (byte)(encrypted[j] >> 16), (byte)(encrypted[j] >> 24));
                });
                for (int i = colorOffset; i < blocks.Length; i += blockBytes) {
                    blocks[i] = 255; blocks[i + 1] = 255; blocks[i + 2] = 0; blocks[i + 3] = 0;
                }
                Array.Copy(blocks, 0, raw, destination, blocks.Length);
                destination += blocks.Length;
                result.Texture2.SetPixels32(pixels, m);
            }
            result.Texture1.LoadRawTextureData(raw);
            result.Texture1.filterMode = FilterMode.Point;
            result.Texture1.anisoLevel = 0;
            return result;
        }
    }

    public class DXT1Format : DXTFormat
    {
        protected override bool HasAlpha => false;
        public override bool CanHandle(TextureFormat format) => format == TextureFormat.DXT1 || format == TextureFormat.DXT1Crunched;
    }

    public class DXT5Format : DXTFormat
    {
        protected override bool HasAlpha => true;
        public override bool CanHandle(TextureFormat format) => format == TextureFormat.DXT5 || format == TextureFormat.DXT5Crunched;
    }
}
#endif
