#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Shell.Protector
{
    // Every BC7 block is normalized to a 32-byte record (BC7Codec) and encrypted with the first eight words of its own
    // ChaCha keystream block. The records of all mip levels go into one RGBA32 atlas, which BC7.cginc decodes.
    public sealed class BC7Format : BaseTextureFormat
    {
        public override bool CanHandle(TextureFormat format) => format == TextureFormat.BC7;

        public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm)
        {
            if (!(algorithm is Chacha20 chacha))
                throw new ArgumentException($"{texture.name}: BC7 textures can only be encrypted with ChaCha.");
            var layout = new BC7TextureLayout(texture, texture.mipmapCount);
            byte[] atlas = new byte[checked((int)layout.AtlasBytes)];
            for (int m = 0; m < layout.MipCount; ++m)
            {
                int mip = m;
                int width = Math.Max(1, texture.width >> mip), height = Math.Max(1, texture.height >> mip);
                int blockWidth = (width + 3) / 4, blockHeight = (height + 3) / 4;
                byte[] source = texture.GetPixelData<byte>(mip).ToArray();
                Parallel.For(0, blockHeight, row =>
                {
                    uint[] unitKey = ConvertKeyToUInt(key);
                    for (int x = 0; x < blockWidth; ++x)
                    {
                        int block = row * blockWidth + x;
                        var record = atlas.AsSpan((layout.MipBlockOffsets[mip] + block) * BC7Codec.RecordBytes, BC7Codec.RecordBytes);
                        BC7Codec.Normalize(source.AsSpan(block * BC7Codec.SourceBlockBytes, BC7Codec.SourceBlockBytes), record);
                        unitKey[3] = GetUnitKey(key, (uint)block, mip);
                        chacha.XorKeyStream(MemoryMarshal.Cast<byte, uint>(record), unitKey);
                    }
                });
            }
            var result = new Texture2D(layout.AtlasWidth, layout.AtlasHeight, TextureFormat.RGBA32, false, true);
            result.name = texture.name + "_BC7Encrypted";
            result.filterMode = FilterMode.Point;
            result.anisoLevel = 0;
            result.wrapMode = TextureWrapMode.Clamp;
            result.LoadRawTextureData(atlas);
            result.Apply(false, false);
            return new EncryptResult { Texture1 = result, Layout = layout };
        }

        public override void SetFormatKeywords(Material material)
        {
            material.EnableKeyword(ShaderProperties.Format0Keyword);
            material.EnableKeyword(ShaderProperties.Format1Keyword);
        }

        // BC7.cginc addresses the atlas with SetLayoutProperties instead of the mip offset tables.
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
