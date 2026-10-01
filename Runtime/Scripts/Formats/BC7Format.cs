#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;

namespace Shell.Protector
{
    public sealed class BC7Format : BaseTextureFormat
    {
        public override bool CanHandle(TextureFormat format) => format == TextureFormat.BC7;

        static void RequireChacha(Texture2D texture, bool chacha)
        {
            if (!chacha)
                throw new ArgumentException($"{texture.name}: BC7 requires ChaCha8. Switch the ShellProtector encryption algorithm from XXTEA to ChaCha8.");
        }

        internal override void Validate(Texture2D texture, int mipCount, ShellProtectorAlgorithm algorithm)
        {
            RequireChacha(texture, algorithm == ShellProtectorAlgorithm.Chacha);
            _ = new BC7TextureLayout(texture, mipCount);
        }

        internal override (int width, int height, bool fullChain) MipReference(Texture2D texture) =>
            (texture.width, texture.height, true);

        internal override int FallbackSize(Texture2D texture, int requestedSize) =>
            requestedSize > 1 && (texture.width < 128 || texture.height < 128) ? 1 : requestedSize;

        // BC7 generates an independent nonce inside Encrypt for every result, including direct callers.
        internal override void PrepareNonce(IEncryptor algorithm, int materialId) { }

        public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm)
        {
            RequireChacha(texture, algorithm is Chacha20);
            var chacha = (Chacha20)algorithm;
            var layout = new BC7TextureLayout(texture, texture.mipmapCount);
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(chacha.Nonce);
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
            return new EncryptResult(algorithm) { Texture1 = result, Layout = layout };
        }

        public override void SetFormatKeywords(Material material)
        {
            material.EnableKeyword(ShaderProperties.Format0Keyword);
            material.EnableKeyword(ShaderProperties.Format1Keyword);
        }

        public override (int, int) CalculateOffsets(Texture2D texture) => (0, 0);

        internal override void ConfigureMaterial(Material material, Texture2D original, EncryptResult encrypted)
        {
            SetFormatKeywords(material);
            var layout = encrypted.Layout;
            material.SetInteger(ShaderProperties.BC7LayoutVersion, 1);
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
    }
}
#endif
