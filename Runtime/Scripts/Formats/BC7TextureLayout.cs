using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Shell.Protector
{
    /// <summary>Public addressing metadata; image content lives only in the encrypted atlas.</summary>
    public sealed class BC7TextureLayout
    {
        public const int MaxMipCount = 13;
        public readonly int Width, Height, MipCount, AtlasWidth, AtlasHeight;
        public readonly long RgbaBytes;
        public readonly bool IsSrgb;
        public readonly TextureWrapMode WrapU, WrapV;
        public readonly int[] MipBlockOffsets;
        public long AtlasBytes => (long)AtlasWidth * AtlasHeight * 4;
        public int BlockCount { get; }

        public BC7TextureLayout(Texture2D source, int mipCount)
        {
            Width = source.width;
            Height = source.height;
            MipCount = mipCount;
            if (Width > 4096 || Height > 4096 || mipCount < 1 || mipCount > MaxMipCount)
                throw new ArgumentException($"{source.name}: BC7 supports textures up to 4096 pixels per axis and 13 mip levels.");
            IsSrgb = GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat);
            WrapU = source.wrapModeU;
            WrapV = source.wrapModeV;
            MipBlockOffsets = new int[mipCount];
            int blocks = 0;
            long rgba = 0;
            for (int m = 0; m < mipCount; ++m)
            {
                int w = Math.Max(1, Width >> m), h = Math.Max(1, Height >> m);
                MipBlockOffsets[m] = blocks;
                blocks += ((w + 3) / 4) * ((h + 3) / 4);
                rgba += (long)w * h * 4;
            }
            BlockCount = blocks;
            RgbaBytes = rgba;
            int words = blocks * (BC7Codec.RecordBytes / 4);
            AtlasWidth = Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(words)));
            AtlasHeight = (words + AtlasWidth - 1) / AtlasWidth;
        }

    }
}
