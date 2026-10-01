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

        public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm)
        {
            if (!(algorithm is Chacha20 chacha))
                throw new ArgumentException($"{texture.name}: BC7 requires ChaCha8. Switch the ShellProtector encryption algorithm from XXTEA to ChaCha8.");
            var layout = new EncryptedTextureLayout(texture, texture.mipmapCount);
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
            return new EncryptResult { Texture1 = result, Layout = layout };
        }

        public override void SetFormatKeywords(Material material)
        {
            material.EnableKeyword(ShaderProperties.Format0Keyword);
            material.EnableKeyword(ShaderProperties.Format1Keyword);
        }

        public override (int, int) CalculateOffsets(Texture2D texture) => (0, 0);
    }
}
#endif
