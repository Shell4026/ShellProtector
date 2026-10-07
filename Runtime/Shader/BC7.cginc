#pragma once

#define _SHELL_PROTECTOR_DATA_LENGTH 8
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

int GetBlockIndex(float2 uv, int mip)
{
    uint2 p = BC7PixelCoord(uv, mip);
    return (p.y >> 2) * ((BC7MipSize(mip).x + 3u) >> 2) + (p.x >> 2);
}

uint BC7MipOffset(int mip)
{
    if (mip < 4) return (uint)_ShellMipOffsets0[mip];
    if (mip < 8) return (uint)_ShellMipOffsets1[mip - 4];
    if (mip < 12) return (uint)_ShellMipOffsets2[mip - 8];
    return (uint)_ShellMipOffsets3[mip - 12];
}

void GetData(Texture2D atlas, SamplerState unusedSampler, inout uint data[8], float2 uv, int mip)
{
    uint width, height;
    atlas.GetDimensions(width, height);
    // Layout guarantees a power-of-two width. Avoid two integer divisions per word.
    uint widthMask = width - 1u, widthShift = firstbithigh(width);
    uint address = (BC7MipOffset(mip) + (uint)GetBlockIndex(uv, mip)) * 8u;
    [unroll]
    for (uint i = 0; i < 8; ++i)
    {
        uint position = address + i;
        uint4 bytes = (uint4)round(atlas.Load(int3(position & widthMask, position >> widthShift, 0)) * 255.0);
        data[i] = bytes.x | (bytes.y << 8) | (bytes.z << 16) | (bytes.w << 24);
    }
}

uint BC7ReadBits(in uint data[8], uint start, uint count)
{
    uint word = start >> 5, shift = start & 31u;
    uint result = SelectWord(data, word) >> shift;
    if (shift + count > 32u) result |= SelectWord(data, word + 1u) << (32u - shift);
    return result & ((1u << count) - 1u);
}

// The four bytes from byte o of the record, so one endpoint in one read. The channels of an endpoint are bytes in a row;
// with three channels the fourth byte belongs to the next endpoint and is ignored. Endpoints end before byte 18.
uint BC7Endpoint(in uint data[8], uint o)
{
    uint word = o >> 2, shift = (o & 3u) * 8u;
    uint low = SelectWord(data, word);
    if (shift == 0u) return low;
    return (low >> shift) | (SelectWord(data, word + 1u) << (32u - shift));
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

float4 GetPixel(Texture2D unusedTex0, Texture2D unusedTex1, SamplerState unusedSampler, in uint data[8], float2 uv, int mip)
{
    uint2 p = BC7PixelCoord(uv, mip);
    uint meta = (data[4] >> 16) & 255u;
    uint mode = meta & 7u, rotation = (meta >> 3) & 3u, selector = (meta >> 5) & 1u;
    uint code = BC7ReadBits(data, 160u + 6u * ((p.y & 3u) * 4u + (p.x & 3u)), 6u);
    bool dual = mode == 4u || mode == 5u;
    uint subsets = mode == 0u || mode == 2u ? 3u : mode == 1u || mode == 3u || mode == 7u ? 2u : 1u;
    // A wrong key is allowed to reach the decoder in the tester; keep all reads within the record.
    uint subset = dual ? 0u : min(code >> 4, subsets - 1u);
    uint colorPrecision = mode == 0u || mode == 1u ? 3u : mode == 6u ? 4u : 2u;
    uint alphaPrecision = colorPrecision;
    if (mode == 4u) { colorPrecision = selector == 0u ? 2u : 3u; alphaPrecision = selector == 0u ? 3u : 2u; }
    uint wc = BC7Weight(dual ? code & 7u : code & 15u, colorPrecision);
    uint wa = dual ? BC7Weight(code >> 3, alphaPrecision) : wc;
    uint stride = mode < 4u ? 3u : 4u, start = subset * 2u * stride;
    // Modes 0-3 have no alpha: both endpoints are opaque.
    uint opaque = mode < 4u ? 0xff000000u : 0u;
    uint endpointA = BC7Endpoint(data, start) | opaque;
    uint endpointB = BC7Endpoint(data, start + stride) | opaque;
    uint4 decoded;
    [unroll]
    for (uint c = 0; c < 4; ++c)
    {
        uint a = (endpointA >> (c * 8u)) & 255u;
        uint b = (endpointB >> (c * 8u)) & 255u;
        uint weight = c == 3u ? wa : wc;
        decoded[c] = ((64u - weight) * a + weight * b + 32u) >> 6;
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
