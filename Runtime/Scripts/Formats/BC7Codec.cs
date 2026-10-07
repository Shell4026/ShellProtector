using System;

namespace Shell.Protector
{
    /// <summary>Losslessly splits one BC7 block into a 16-byte endpoint record and one 6-bit code per pixel.</summary>
    public static class BC7Codec
    {
        public const int SourceBlockBytes = 16;
        // Mode, rotation and selector in 6 bits, then every endpoint at its own precision (p-bit included): R, G, B, and A
        // for modes 4-7. The largest blocks (modes 3 and 7) take 6 + 96 bits, so four records share one 64-byte keystream.
        public const int RecordBytes = 16;
        public const int PixelCount = 16;
        static readonly byte[] Subsets = { 3, 2, 3, 2, 1, 1, 1, 2 };
        static readonly byte[] PartitionBits = { 4, 6, 6, 6, 0, 0, 0, 6 };
        static readonly byte[] ColorBits = { 4, 6, 5, 7, 5, 7, 7, 5 };
        static readonly byte[] AlphaBits = { 0, 0, 0, 0, 6, 8, 7, 5 };
        static readonly byte[] IndexBits = { 3, 3, 2, 2, 2, 2, 4, 2 };
        static readonly bool[] HasP = { true, true, false, true, false, false, true, true };

        // codes: per pixel (row-major in the block), subset << 4 | index, or scalar << 3 | vector for modes 4 and 5.
        public static void Normalize(ReadOnlySpan<byte> source, Span<byte> record, Span<byte> codes)
        {
            if (source.Length != SourceBlockBytes || record.Length != RecordBytes || codes.Length != PixelCount)
                throw new ArgumentException("BC7 normalization requires a 16-byte block, a 16-byte record and 16 codes.");
            record.Clear();
            codes.Clear();
            var reader = new BitReader(source);
            var writer = new BitWriter(record);
            int mode = 0;
            while (mode < 8 && reader.Read(1) == 0) ++mode;
            if (mode == 8)
            {
                // The native BC7 decoder defines the reserved zero prefix as transparent black: mode 6 with zero endpoints.
                writer.Write(6, 6);
                return;
            }

            int subsets = Subsets[mode], count = subsets * 2;
            int partition = reader.Read(PartitionBits[mode]);
            bool dual = mode == 4 || mode == 5;
            int rotation = dual ? reader.Read(2) : 0;
            int selector = mode == 4 ? reader.Read(1) : 0;
            int channels = AlphaBits[mode] == 0 ? 3 : 4;
            Span<int> endpoints = stackalloc int[24];
            for (int c = 0; c < channels; ++c)
                for (int e = 0; e < count; ++e)
                    endpoints[e * channels + c] = reader.Read(c == 3 ? AlphaBits[mode] : ColorBits[mode]);

            Span<int> pbits = stackalloc int[6];
            pbits.Clear();
            if (HasP[mode])
            {
                if (mode == 1)
                    for (int s = 0; s < subsets; ++s)
                        pbits[s * 2] = pbits[s * 2 + 1] = reader.Read(1);
                else
                    for (int e = 0; e < count; ++e)
                        pbits[e] = reader.Read(1);
            }
            writer.Write(mode | (rotation << 3) | (selector << 5), 6);
            for (int e = 0; e < count; ++e)
                for (int c = 0; c < channels; ++c)
                {
                    int precision = c == 3 ? AlphaBits[mode] : ColorBits[mode];
                    int value = endpoints[e * channels + c];
                    if (HasP[mode]) { value = (value << 1) | pbits[e]; ++precision; }
                    writer.Write(value, precision);
                }

            if (dual)
            {
                Span<int> first = stackalloc int[16];
                for (int pixel = 0; pixel < 16; ++pixel)
                    first[pixel] = reader.Read(2 - (pixel == 0 ? 1 : 0));
                for (int pixel = 0; pixel < 16; ++pixel)
                {
                    int second = reader.Read((mode == 4 ? 3 : 2) - (pixel == 0 ? 1 : 0));
                    int vector = selector == 0 ? first[pixel] : second;
                    int scalar = selector == 0 ? second : first[pixel];
                    codes[pixel] = (byte)(vector | (scalar << 3));
                }
            }
            else
            {
                uint partitionMap = subsets == 3 ? Partition3[partition] : subsets == 2 ? Partition2[partition] : 0;
                for (int pixel = 0; pixel < 16; ++pixel)
                {
                    int subset = (int)((partitionMap >> (pixel * 2)) & 3);
                    int anchor = subset == 0 ? 0 : subsets == 2 ? Anchor2Subset1[partition]
                        : subset == 1 ? Anchor3Subset1[partition] : Anchor3Subset2[partition];
                    int index = reader.Read(IndexBits[mode] - (pixel == anchor ? 1 : 0));
                    codes[pixel] = (byte)(index | (subset << 4));
                }
            }
        }

        ref struct BitWriter
        {
            readonly Span<byte> data;
            int bit;
            public BitWriter(Span<byte> data) { this.data = data; bit = 0; }
            public void Write(int value, int count)
            {
                for (int i = 0; i < count; ++i, ++bit)
                    if (((value >> i) & 1) != 0)
                        data[bit >> 3] |= (byte)(1 << (bit & 7));
            }
        }

        ref struct BitReader
        {
            readonly ReadOnlySpan<byte> data;
            int bit;
            public BitReader(ReadOnlySpan<byte> data) { this.data = data; bit = 0; }
            public int Read(int count)
            {
                if (count == 0) return 0;
                int offset = bit >> 3, shift = bit & 7, value = data[offset];
                if (shift + count > 8) value |= data[offset + 1] << 8;
                bit += count;
                return (value >> shift) & ((1 << count) - 1);
            }
        }

        // Derived from Microsoft DirectXTex BC6HBC7.cpp, MIT licensed.
        // Copyright (c) Microsoft Corporation.
        // Revision: 603bfa32b8dc7272eace963cdd05a03016224b61
        // The full license and pinned source are in ThirdPartyNotices.txt.
        // Subset at (x,y) = (PartitionN[shape] >> (2 * (x + 4*y))) & 3.
        // Subset 0 anchor is always texel 0. Other anchor arrays are indexed by shape.
        static readonly uint[] Partition2 = {
            0x50505050u, 0x40404040u, 0x54545454u, 0x54505040u, 0x50404000u, 0x55545450u, 0x55545040u, 0x54504000u,
            0x50400000u, 0x55555450u, 0x55544000u, 0x54400000u, 0x55555440u, 0x55550000u, 0x55555500u, 0x55000000u,
            0x55150100u, 0x00004054u, 0x15010000u, 0x00405054u, 0x00004050u, 0x15050100u, 0x05010000u, 0x40505054u,
            0x00404050u, 0x05010100u, 0x14141414u, 0x05141450u, 0x01155440u, 0x00555500u, 0x15014054u, 0x05414150u,
            0x44444444u, 0x55005500u, 0x11441144u, 0x05055050u, 0x05500550u, 0x11114444u, 0x41144114u, 0x44111144u,
            0x15055054u, 0x01055040u, 0x05041050u, 0x05455150u, 0x14414114u, 0x50050550u, 0x41411414u, 0x00141400u,
            0x00041504u, 0x00105410u, 0x10541000u, 0x04150400u, 0x50410514u, 0x41051450u, 0x05415014u, 0x14054150u,
            0x41050514u, 0x41505014u, 0x40011554u, 0x54150140u, 0x50505500u, 0x00555050u, 0x15151010u, 0x54540404u,
        };
        static readonly byte[] Anchor2Subset1 = {
            15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
            15, 2, 8, 2, 2, 8, 8, 15, 2, 8, 2, 2, 8, 8, 2, 2,
            15, 15, 6, 8, 2, 8, 15, 15, 2, 8, 2, 2, 2, 15, 15, 6,
            6, 2, 6, 8, 15, 15, 2, 2, 15, 15, 15, 15, 15, 2, 2, 15,
        };
        static readonly uint[] Partition3 = {
            0xAA685050u, 0x6A5A5040u, 0x5A5A4200u, 0x5450A0A8u, 0xA5A50000u, 0xA0A05050u, 0x5555A0A0u, 0x5A5A5050u,
            0xAA550000u, 0xAA555500u, 0xAAAA5500u, 0x90909090u, 0x94949494u, 0xA4A4A4A4u, 0xA9A59450u, 0x2A0A4250u,
            0xA5945040u, 0x0A425054u, 0xA5A5A500u, 0x55A0A0A0u, 0xA8A85454u, 0x6A6A4040u, 0xA4A45000u, 0x1A1A0500u,
            0x0050A4A4u, 0xAAA59090u, 0x14696914u, 0x69691400u, 0xA08585A0u, 0xAA821414u, 0x50A4A450u, 0x6A5A0200u,
            0xA9A58000u, 0x5090A0A8u, 0xA8A09050u, 0x24242424u, 0x00AA5500u, 0x24924924u, 0x24499224u, 0x50A50A50u,
            0x500AA550u, 0xAAAA4444u, 0x66660000u, 0xA5A0A5A0u, 0x50A050A0u, 0x69286928u, 0x44AAAA44u, 0x66666600u,
            0xAA444444u, 0x54A854A8u, 0x95809580u, 0x96969600u, 0xA85454A8u, 0x80959580u, 0xAA141414u, 0x96960000u,
            0xAAAA1414u, 0xA05050A0u, 0xA0A5A5A0u, 0x96000000u, 0x40804080u, 0xA9A8A9A8u, 0xAAAAAA44u, 0x2A4A5254u,
        };
        static readonly byte[] Anchor3Subset1 = {
            3, 3, 15, 15, 8, 3, 15, 15, 8, 8, 6, 6, 6, 5, 3, 3,
            3, 3, 8, 15, 3, 3, 6, 10, 5, 8, 8, 6, 8, 5, 15, 15,
            8, 15, 3, 5, 6, 10, 8, 15, 15, 3, 15, 5, 15, 15, 15, 15,
            3, 15, 5, 5, 5, 8, 5, 10, 5, 10, 8, 13, 15, 12, 3, 3,
        };
        static readonly byte[] Anchor3Subset2 = {
            15, 8, 8, 3, 15, 15, 3, 8, 15, 15, 15, 15, 15, 15, 15, 8,
            15, 8, 15, 3, 15, 8, 15, 8, 3, 15, 6, 10, 15, 15, 10, 8,
            15, 3, 15, 10, 10, 8, 9, 10, 6, 15, 8, 15, 3, 6, 6, 8,
            15, 3, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 3, 15, 15, 8,
        };
    }
}
