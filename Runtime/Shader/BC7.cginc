#pragma once

// tex0 holds the plain 6-bit code of every pixel (R8, the source's mip chain). tex1 is the encrypted atlas of 16-byte endpoint
// records, one per 4x4 block. One ChaCha keystream block covers 2x2 blocks: block (x & 1) + 2 * (y & 1) of the unit uses
// keystream words 4k to 4k + 3 (BC7Format).
#define _SHELL_PROTECTOR_DATA_LENGTH 16
#define _SHELL_PROTECTOR_INDEX_ALIGNMENT 0

float4 _ShellSourceTexelSize;
float4 _ShellSourceSampling; // mip count, sRGB, wrap U, wrap V
float4 _ShellMipOffsets0, _ShellMipOffsets1, _ShellMipOffsets2, _ShellMipOffsets3;

uint2 BC7MipSize(int mip)
{
    return max((uint2)_ShellSourceTexelSize.zw >> mip, 1u);
}

float BC7Wrap(float coordinate, uint mode)
{
    if (mode == 1u) return saturate(coordinate); // Clamp
    if (mode == 2u) return 1.0 - abs(frac(coordinate * 0.5) * 2.0 - 1.0); // Mirror
    if (mode == 3u) return saturate(abs(coordinate)); // MirrorOnce
    return frac(coordinate);
}

uint2 BC7PixelCoord(float2 uv, int mip)
{
    uint2 size = BC7MipSize(mip);
    float2 wrapped = float2(BC7Wrap(uv.x, (uint)_ShellSourceSampling.z), BC7Wrap(uv.y, (uint)_ShellSourceSampling.w));
    return min((uint2)floor(wrapped * size), size - 1u);
}

uint BC7BlocksPerRow(int mip)
{
    return (BC7MipSize(mip).x + 3u) >> 2;
}

// The keystream unit of a pixel: 2x2 blocks.
int BC7Unit(uint2 p, int mip)
{
    uint unitsPerRow = (BC7BlocksPerRow(mip) + 1u) >> 1;
    return (p.y >> 3) * unitsPerRow + (p.x >> 3);
}

// Which record of its unit the pixel's block is.
uint BC7UnitLocal(uint2 p)
{
    return ((p.x >> 2) & 1u) | (((p.y >> 2) & 1u) << 1);
}

int GetBlockIndex(float2 uv, int mip)
{
    return BC7Unit(BC7PixelCoord(uv, mip), mip);
}

uint BC7MipOffset(int mip)
{
    if (mip < 4) return (uint)_ShellMipOffsets0[mip];
    if (mip < 8) return (uint)_ShellMipOffsets1[mip - 4];
    if (mip < 12) return (uint)_ShellMipOffsets2[mip - 8];
    return (uint)_ShellMipOffsets3[mip - 12];
}

// The records are loaded per pixel (BC7DecodeTexel), so the unit's data is only its keystream.
void GetData(Texture2D unusedTex, SamplerState unusedSampler, inout uint data[16], float2 uv, int mip)
{
    [unroll]
    for (int i = 0; i < 16; ++i)
        data[i] = 0;
}

// Keystream words 4k to 4k + 3, with constant indices only (see SelectWord).
uint4 BC7SelectRecordStream(const uint data[16], uint k)
{
    const uint4 a = (k & 1u) != 0 ? uint4(data[4], data[5], data[6], data[7]) : uint4(data[0], data[1], data[2], data[3]);
    const uint4 b = (k & 1u) != 0 ? uint4(data[12], data[13], data[14], data[15]) : uint4(data[8], data[9], data[10], data[11]);
    return (k & 2u) != 0 ? b : a;
}

uint4 BC7LoadRecord(Texture2D atlas, uint block)
{
    uint width, height;
    atlas.GetDimensions(width, height);
    // Layout guarantees a power-of-two width. Avoid an integer division per word.
    uint widthMask = width - 1u, widthShift = firstbithigh(width);
    uint address = block * 4u;
    uint4 record;
    [unroll]
    for (uint i = 0; i < 4; ++i)
    {
        uint position = address + i;
        uint4 bytes = (uint4)round(atlas.Load(int3(position & widthMask, position >> widthShift, 0)) * 255.0);
        record[i] = bytes.x | (bytes.y << 8) | (bytes.z << 16) | (bytes.w << 24);
    }
    return record;
}

uint BC7RecordWord(uint4 record, uint word)
{
    const uint2 pair = (word & 1u) != 0 ? record.yw : record.xz;
    return (word & 2u) != 0 ? pair.y : pair.x;
}

// The 32 bits from bit start of the record. An endpoint has at most 32 bits and starts before bit 82, so this holds a
// whole endpoint and never reads past the last word.
uint BC7RecordWindow(uint4 record, uint start)
{
    uint word = start >> 5, shift = start & 31u;
    uint high = shift != 0u ? BC7RecordWord(record, word + 1u) << (32u - shift) : 0u;
    return (BC7RecordWord(record, word) >> shift) | high;
}

// A precision-bit endpoint channel widened to 8 bits the way BC7 does: the high bits repeat in the low bits.
uint BC7Expand(uint value, uint precision)
{
    uint widened = (value & ((1u << precision) - 1u)) << (8u - precision);
    return (widened | (widened >> precision)) & 255u;
}

// Endpoint at bit start: R, G, B with colorBits each, then A with alphaBits (opaque when there are none).
uint4 BC7ReadEndpoint(uint4 record, uint start, uint colorBits, uint alphaBits)
{
    uint bits = BC7RecordWindow(record, start);
    uint4 endpoint;
    endpoint.r = BC7Expand(bits, colorBits);
    endpoint.g = BC7Expand(bits >> colorBits, colorBits);
    endpoint.b = BC7Expand(bits >> (2u * colorBits), colorBits);
    endpoint.a = alphaBits == 0u ? 255u : BC7Expand(bits >> (3u * colorBits), alphaBits);
    return endpoint;
}

uint BC7Weight(uint index, uint precision)
{
    if (precision == 2u) { index &= 3u; return index * 21u + (index >> 1); }
    if (precision == 3u)
    {
        uint packed = (index & 4u) == 0u ? 0x1b120900u : 0x40372e25u;
        return (packed >> ((index & 3u) * 8u)) & 255u;
    }
    uint group = (index >> 2) & 3u;
    uint packed = group == 0u ? 0x0d090400u : group == 1u ? 0x1e1a1511u : group == 2u ? 0x2f2b2622u : 0x403c3733u;
    return (packed >> ((index & 3u) * 8u)) & 255u;
}

float BC7SrgbToLinear(float c)
{
    return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4);
}

// One texel of mip level mip at pixel p, with the keystream words of its record.
float4 BC7DecodeTexel(Texture2D codes, Texture2D atlas, uint4 stream, uint2 p, int mip)
{
    uint4 record = BC7LoadRecord(atlas, BC7MipOffset(mip) + (p.y >> 2) * BC7BlocksPerRow(mip) + (p.x >> 2)) ^ stream;
    uint code = (uint)round(codes.Load(int3(p, mip)).r * 255.0);
    uint mode = record.x & 7u, rotation = (record.x >> 3) & 3u, selector = (record.x >> 5) & 1u;
    bool dual = mode == 4u || mode == 5u;
    // Per mode, one nibble each: subsets, endpoint color bits and endpoint alpha bits (p-bits included).
    uint subsets = (0x21112323u >> (mode * 4u)) & 15u;
    uint colorBits = (0x68758575u >> (mode * 4u)) & 15u;
    uint alphaBits = (0x68860000u >> (mode * 4u)) & 15u;
    // A wrong key is allowed to reach the decoder in the tester; keep all reads within the record.
    uint subset = dual ? 0u : min(code >> 4, subsets - 1u);
    uint colorPrecision = mode == 0u || mode == 1u ? 3u : mode == 6u ? 4u : 2u;
    uint alphaPrecision = colorPrecision;
    if (mode == 4u) { colorPrecision = selector == 0u ? 2u : 3u; alphaPrecision = selector == 0u ? 3u : 2u; }
    uint wc = BC7Weight(dual ? code & 7u : code & 15u, colorPrecision);
    uint wa = dual ? BC7Weight(code >> 3, alphaPrecision) : wc;
    uint endpointBits = 3u * colorBits + alphaBits;
    uint start = 6u + subset * 2u * endpointBits;
    uint4 endpointA = BC7ReadEndpoint(record, start, colorBits, alphaBits);
    uint4 endpointB = BC7ReadEndpoint(record, start + endpointBits, colorBits, alphaBits);
    uint4 decoded;
    [unroll]
    for (uint c = 0; c < 4; ++c)
    {
        uint weight = c == 3u ? wa : wc;
        decoded[c] = ((64u - weight) * endpointA[c] + weight * endpointB[c] + 32u) >> 6;
    }
    if (dual)
    {
        if (rotation == 1u) decoded = decoded.agbr;
        else if (rotation == 2u) decoded = decoded.rabg;
        else if (rotation == 3u) decoded = decoded.rgab;
    }
    float4 color = decoded / 255.0;
    #ifndef UNITY_COLORSPACE_GAMMA
    if (_ShellSourceSampling.y != 0)
        color.rgb = float3(BC7SrgbToLinear(color.r), BC7SrgbToLinear(color.g), BC7SrgbToLinear(color.b));
    #endif
    return color;
}

float4 GetPixel(Texture2D codes, Texture2D atlas, SamplerState unusedSampler, in uint data[16], float2 uv, int mip)
{
    uint2 p = BC7PixelCoord(uv, mip);
    return BC7DecodeTexel(codes, atlas, BC7SelectRecordStream(data, BC7UnitLocal(p)), p, mip);
}
