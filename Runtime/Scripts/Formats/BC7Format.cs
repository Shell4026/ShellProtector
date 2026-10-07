#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Shell.Protector
{
    // Every BC7 block is split into a 16-byte endpoint record and a 6-bit code per pixel (BC7Codec). The records of all mip
    // levels go into one RGBA32 atlas, encrypted with ChaCha: one keystream block covers 2x2 blocks (8x8 texels), four words
    // per record, so a bilinear fetch rarely needs a second keystream. The codes stay plain in an R8 texture with the
    // source's mip chain, the way DXT keeps its index bits: they show the shapes but not the colors.
    public sealed class BC7Format : BaseTextureFormat
    {
        const int RecordWords = BC7Codec.RecordBytes / 4;

        public override bool CanHandle(TextureFormat format) => format == TextureFormat.BC7;

        public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm)
        {
            if (!(algorithm is Chacha20 chacha))
                throw new ArgumentException($"{texture.name}: BC7 textures can only be encrypted with ChaCha.");
            var layout = new BC7TextureLayout(texture, texture.mipmapCount);
            byte[] atlas = new byte[checked((int)layout.AtlasBytes)];
            var codes = new Texture2D(texture.width, texture.height, TextureFormat.R8, layout.MipCount, true);
            codes.name = texture.name + "_BC7Codes";
            codes.filterMode = FilterMode.Point;
            codes.anisoLevel = 0;
            codes.wrapMode = TextureWrapMode.Clamp;
            for (int m = 0; m < layout.MipCount; ++m)
            {
                int mip = m;
                int width = Math.Max(1, texture.width >> mip), height = Math.Max(1, texture.height >> mip);
                int blockWidth = (width + 3) / 4, blockHeight = (height + 3) / 4;
                int unitWidth = (blockWidth + 1) / 2, unitHeight = (blockHeight + 1) / 2;
                byte[] source = texture.GetPixelData<byte>(mip).ToArray();
                byte[] pixelCodes = new byte[width * height];
                Parallel.For(0, unitHeight, unitRow =>
                {
                    uint[] unitKey = ConvertKeyToUInt(key);
                    Span<uint> stream = stackalloc uint[16];
                    Span<byte> blockCodes = stackalloc byte[BC7Codec.PixelCount];
                    for (int unitColumn = 0; unitColumn < unitWidth; ++unitColumn)
                    {
                        stream.Clear();
                        unitKey[3] = GetUnitKey(key, (uint)(unitRow * unitWidth + unitColumn), mip);
                        chacha.XorKeyStream(stream, unitKey);
                        for (int local = 0; local < 4; ++local)
                        {
                            int bx = unitColumn * 2 + (local & 1), by = unitRow * 2 + (local >> 1);
                            if (bx >= blockWidth || by >= blockHeight)
                                continue;
                            int block = by * blockWidth + bx;
                            var record = atlas.AsSpan((layout.MipBlockOffsets[mip] + block) * BC7Codec.RecordBytes, BC7Codec.RecordBytes);
                            BC7Codec.Normalize(source.AsSpan(block * BC7Codec.SourceBlockBytes, BC7Codec.SourceBlockBytes), record, blockCodes);
                            var words = MemoryMarshal.Cast<byte, uint>(record);
                            for (int i = 0; i < RecordWords; ++i)
                                words[i] ^= stream[local * RecordWords + i];
                            for (int pixel = 0; pixel < BC7Codec.PixelCount; ++pixel)
                            {
                                int x = bx * 4 + (pixel & 3), y = by * 4 + (pixel >> 2);
                                if (x < width && y < height)
                                    pixelCodes[y * width + x] = blockCodes[pixel];
                            }
                        }
                    }
                });
                codes.SetPixelData(pixelCodes, mip);
            }
            var endpoints = new Texture2D(layout.AtlasWidth, layout.AtlasHeight, TextureFormat.RGBA32, false, true);
            endpoints.name = texture.name + "_BC7Encrypted";
            endpoints.filterMode = FilterMode.Point;
            endpoints.anisoLevel = 0;
            endpoints.wrapMode = TextureWrapMode.Clamp;
            endpoints.LoadRawTextureData(atlas);
            return new EncryptResult { Texture1 = codes, Texture2 = endpoints, Layout = layout };
        }

        public override void SetFormatKeywords(Material material)
        {
            material.EnableKeyword(ShaderProperties.Format0Keyword);
            material.EnableKeyword(ShaderProperties.Format1Keyword);
        }

        // BC7.cginc addresses the textures with SetLayoutProperties instead of the mip offset tables.
        public override (int, int) CalculateOffsets(Texture2D texture) => (0, 0);

        public static void SetLayoutProperties(Material material, BC7TextureLayout layout)
        {
            material.SetVector(ShaderProperties.SourceTexelSize, new Vector4(1f / layout.Width, 1f / layout.Height, layout.Width, layout.Height));
            material.SetVector(ShaderProperties.SourceSampling, new Vector4(layout.MipCount, layout.IsSrgb ? 1 : 0, (int)layout.WrapU, (int)layout.WrapV));
            for (int i = 0; i < 4; ++i)
            {
                Vector4 offsets = Vector4.zero;
                for (int j = 0; j < 4; ++j)
                    if (i * 4 + j < layout.MipCount) offsets[j] = layout.MipBlockOffsets[i * 4 + j];
                material.SetVector(ShaderProperties.MipOffsetsPrefix + i, offsets);
            }
        }

        // False for a shader injected before BC7 support, which has no properties for the layout.
        public static bool SupportsShader(Shader shader)
        {
            return shader != null && shader.FindPropertyIndex(ShaderProperties.SourceTexelSize) >= 0;
        }
    }
}
#endif
