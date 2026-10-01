#pragma once

#pragma shader_feature_local _SHELL_PROTECTOR_XXTEA
#pragma shader_feature_local _SHELL_PROTECTOR_CHACHA
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT0
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT1
#pragma shader_feature_local _SHELL_PROTECTOR_RIMLIGHT

// Unity also compiles a no-keyword variant while importing shaders.
#if !_SHELL_PROTECTOR_XXTEA && !_SHELL_PROTECTOR_CHACHA
    #define _SHELL_PROTECTOR_CHACHA
#endif

// Format keywords
#if !_SHELL_PROTECTOR_FORMAT0 && !_SHELL_PROTECTOR_FORMAT1
    #define _SHELL_PROTECTOR_DXT
#elif !_SHELL_PROTECTOR_FORMAT0 && _SHELL_PROTECTOR_FORMAT1
    #define _SHELL_PROTECTOR_RGBA
#elif _SHELL_PROTECTOR_FORMAT0 && !_SHELL_PROTECTOR_FORMAT1
    #define _SHELL_PROTECTOR_RGB
#else
    #define _SHELL_PROTECTOR_BC7
#endif

// BC7 is ChaCha-only. Keep Unity's otherwise-unused XXTEA/BC7 import variant compilable;
// the build pipeline rejects that algorithm/format combination before generating assets.
#ifdef _SHELL_PROTECTOR_BC7
    #undef _SHELL_PROTECTOR_XXTEA
    #ifndef _SHELL_PROTECTOR_CHACHA
        #define _SHELL_PROTECTOR_CHACHA
    #endif
#endif

static const uint mw[13] = { 4096, 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };
static const uint mh[13] = { 4096, 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };

float _Key0, _Key1, _Key2, _Key3, _Key4, _Key5, _Key6, _Key7, _Key8, _Key9, _Key10, _Key11, _Key12, _Key13, _Key14, _Key15;

uint _Woffset;
uint _Hoffset;
uint _HashMagic;

#include "Utility.cginc"

// Ciphers
#ifdef _SHELL_PROTECTOR_XXTEA
	#include "XXTEA.cginc"
#endif

#ifdef _SHELL_PROTECTOR_CHACHA
	#include "Chacha.cginc"
#endif

#ifdef _SHELL_PROTECTOR_RGB
	#include "RGB.cginc"
#endif

#ifdef _SHELL_PROTECTOR_RGBA
	#include "RGBA.cginc"
#endif

#ifdef _SHELL_PROTECTOR_DXT
	#include "DXT.cginc"
#endif

#ifdef _SHELL_PROTECTOR_BC7
    #include "BC7.cginc"
#endif

void DecryptData(inout uint data[_SHELL_PROTECTOR_DATA_LENGTH], Texture2D tex0, Texture2D tex1, SamplerState tex0Sampler, float2 uv, int m)
{
#if defined(_SHELL_PROTECTOR_DXT) || defined(_SHELL_PROTECTOR_BLOCK_STREAM) || defined(_SHELL_PROTECTOR_BC7)
	const int idx = GetBlockIndex(uv, m);
#else
	const int idx = GetIndex(uv, m);
#endif
	// Mip level in the top 8 bits (indices stay below 2^24) so mip levels never share a keystream.
	const uint key[4] = 
	{
		((uint)round(_Key0) | ((uint)round(_Key1) << 8) | ((uint)round(_Key2) << 16) | ((uint)round(_Key3) << 24)),
		((uint)round(_Key4) | ((uint)round(_Key5) << 8) | ((uint)round(_Key6) << 16) | ((uint)round(_Key7) << 24)),
		((uint)round(_Key8) | ((uint)round(_Key9) << 8) | ((uint)round(_Key10) << 16) | ((uint)round(_Key11) << 24)),
		((uint)round(_Key12) | ((uint)round(_Key13) << 8) | ((uint)round(_Key14) << 16) | ((uint)round(_Key15) << 24)) ^ (uint)((idx >> _SHELL_PROTECTOR_INDEX_ALIGNMENT) << _SHELL_PROTECTOR_INDEX_ALIGNMENT) ^ ((uint)m << 24)
	};
#ifdef _SHELL_PROTECTOR_DXT
    GetData(tex1, tex0Sampler, data, uv, m);
#else
    GetData(tex0, tex0Sampler, data, uv, m);
#endif
	Decrypt(data, key);
}

float4 DecryptTexture(Texture2D tex0, Texture2D tex1, SamplerState tex0Sampler, float2 uv, int m)
{
	uint data[_SHELL_PROTECTOR_DATA_LENGTH];
	DecryptData(data, tex0, tex1, tex0Sampler, uv, m);
    return GetPixel(tex0, tex0Sampler, data, uv, m);
}

int GetTextureMip(Texture2D mipTex, SamplerState mipSamp, float2 uv)
{
    const int mip = (int)round(mipTex.Sample(mipSamp, uv).r * 255 / 10);
#ifdef _SHELL_PROTECTOR_BC7
    return clamp(mip, 0, (int)_ShellSourceSampling.x - 1);
#else
    const int levels[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };
    return levels[mip];
#endif
}

float4 DecryptTextureBox(Texture2D tex0, Texture2D tex1, SamplerState texSampler, float4 texSize, Texture2D mipTex, SamplerState mipSamp, float2 uv)
{
    return DecryptTexture(tex0, tex1, texSampler, uv, GetTextureMip(mipTex, mipSamp, uv));
}

#if defined(_SHELL_PROTECTOR_DXT) || defined(_SHELL_PROTECTOR_BLOCK_STREAM) || defined(_SHELL_PROTECTOR_BC7)
// Legacy formats retain only the selected word; BC7 resolves a pixel while its full record is available.
#ifdef _SHELL_PROTECTOR_BC7
    #define SHELL_BILINEAR_TAP float4
#else
    #define SHELL_BILINEAR_TAP uint
#endif

SHELL_BILINEAR_TAP ReadBilinearTap(in uint data[_SHELL_PROTECTOR_DATA_LENGTH], Texture2D tex, SamplerState samp, float2 uv, int mip, bool sameUnit)
{
#ifdef _SHELL_PROTECTOR_BC7
    if (sameUnit) return GetPixel(tex, samp, data, uv, mip);
    return 0;
#else
    return data[GetBlockLocalIndex(uv, mip)];
#endif
}

float4 ResolveBilinearTap(SHELL_BILINEAR_TAP tap, Texture2D tex, SamplerState samp, float2 uv, int mip)
{
#ifdef _SHELL_PROTECTOR_BC7
    return tap;
#else
    return GetBlockPixel(tex, samp, tap, uv, mip);
#endif
}
#endif

float4 DecryptTextureBilinear(Texture2D tex0, Texture2D tex1, SamplerState texSampler, float4 originalTexSize, Texture2D mipTex, SamplerState mipSamp, float2 uv)
{
    const int mip = GetTextureMip(mipTex, mipSamp, uv);
#ifdef _SHELL_PROTECTOR_BC7
    const float2 size = BC7MipSize(mip);
    const float2 position = uv * size - 0.5;
    const float2 baseUV = (floor(position) + 0.5) / size;
    const float2 uvUnit = 1.0 / size;
    const float2 f = frac(position);
#else
    // Preserve legacy sampling: its tap spacing is expressed in mip-zero texels.
    const float2 uvUnit = originalTexSize.xy;
    const float2 baseUV = uv - 0.5 * uvUnit;
    const float2 f = frac(baseUV * originalTexSize.zw);
#endif
    const float2 uv00 = baseUV;
    const float2 uv10 = baseUV + float2(uvUnit.x, 0);
    const float2 uv01 = baseUV + float2(0, uvUnit.y);
    const float2 uv11 = baseUV + uvUnit;

#if defined(_SHELL_PROTECTOR_DXT) || defined(_SHELL_PROTECTOR_BLOCK_STREAM) || defined(_SHELL_PROTECTOR_BC7)
    const int unit00 = GetBlockIndex(uv00, mip) >> _SHELL_PROTECTOR_INDEX_ALIGNMENT;
    const int unit10 = GetBlockIndex(uv10, mip) >> _SHELL_PROTECTOR_INDEX_ALIGNMENT;
    const int unit01 = GetBlockIndex(uv01, mip) >> _SHELL_PROTECTOR_INDEX_ALIGNMENT;
    const int unit11 = GetBlockIndex(uv11, mip) >> _SHELL_PROTECTOR_INDEX_ALIGNMENT;
    uint data[_SHELL_PROTECTOR_DATA_LENGTH];
    DecryptData(data, tex0, tex1, texSampler, uv00, mip);
    SHELL_BILINEAR_TAP tap00 = ReadBilinearTap(data, tex0, texSampler, uv00, mip, true);
    SHELL_BILINEAR_TAP tap10 = ReadBilinearTap(data, tex0, texSampler, uv10, mip, unit10 == unit00);
    SHELL_BILINEAR_TAP tap01 = ReadBilinearTap(data, tex0, texSampler, uv01, mip, unit01 == unit00);
    SHELL_BILINEAR_TAP tap11 = ReadBilinearTap(data, tex0, texSampler, uv11, mip, unit11 == unit00);
    [branch]
    if (unit10 != unit00)
    {
        DecryptData(data, tex0, tex1, texSampler, uv10, mip);
        tap10 = ReadBilinearTap(data, tex0, texSampler, uv10, mip, true);
        if (unit11 == unit10) tap11 = ReadBilinearTap(data, tex0, texSampler, uv11, mip, true);
    }
    [branch]
    if (unit01 != unit00)
    {
        DecryptData(data, tex0, tex1, texSampler, uv01, mip);
        tap01 = ReadBilinearTap(data, tex0, texSampler, uv01, mip, true);
        if (unit11 == unit01) tap11 = ReadBilinearTap(data, tex0, texSampler, uv11, mip, true);
    }
    [branch]
    if (unit11 != unit00 && unit11 != unit10 && unit11 != unit01)
    {
        DecryptData(data, tex0, tex1, texSampler, uv11, mip);
        tap11 = ReadBilinearTap(data, tex0, texSampler, uv11, mip, true);
    }
    const float4 c00 = ResolveBilinearTap(tap00, tex0, texSampler, uv00, mip);
    const float4 c10 = ResolveBilinearTap(tap10, tex0, texSampler, uv10, mip);
    const float4 c01 = ResolveBilinearTap(tap01, tex0, texSampler, uv01, mip);
    const float4 c11 = ResolveBilinearTap(tap11, tex0, texSampler, uv11, mip);
#else
    const float4 c00 = DecryptTexture(tex0, tex1, texSampler, uv00, mip);
    const float4 c10 = DecryptTexture(tex0, tex1, texSampler, uv10, mip);
    const float4 c01 = DecryptTexture(tex0, tex1, texSampler, uv01, mip);
    const float4 c11 = DecryptTexture(tex0, tex1, texSampler, uv11, mip);
#endif
    return lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
}
inline uint SimpleHash(int data[16])
{
    uint hash = 0x811C9DC5u;
	hash *= _HashMagic;

    [unroll]
    for (int i = 0; i < 16; i++)
    {
        uint k = (uint)(data[i] & 0xFF);

        k *= 0xcc9e2d51u;
        k = (k << 15) | (k >> 17);
        k *= 0x1b873593u;

        hash ^= k;
        hash = (hash << 13) | (hash >> 19);
        hash = hash * 5u + 0xe6546b64u;
    }

    hash ^= 16u;
    hash ^= (hash >> 16);
    hash *= 0x85ebca6bu;
    hash ^= (hash >> 13);
    hash *= 0xc2b2ae35u;
    hash ^= (hash >> 16);

    return hash;
}

inline bool IsDecrypted() {
	const int key[16] = 
	{
		(int)round(_Key0), (int)round(_Key1), (int)round(_Key2), (int)round(_Key3),
		(int)round(_Key4), (int)round(_Key5), (int)round(_Key6), (int)round(_Key7),
		(int)round(_Key8), (int)round(_Key9), (int)round(_Key10), (int)round(_Key11),
		(int)round(_Key12), (int)round(_Key13), (int)round(_Key14), (int)round(_Key15)
	};
	return SimpleHash(key) == _PasswordHash;
}
