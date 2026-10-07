#pragma once

#pragma shader_feature_local _SHELL_PROTECTOR_XXTEA
#pragma shader_feature_local _SHELL_PROTECTOR_CHACHA
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT0
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT1
#pragma shader_feature_local _SHELL_PROTECTOR_RIMLIGHT
#pragma shader_feature_local _SHELL_PROTECTOR_POINT

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

// BC7 is ChaCha only. Unity still compiles the XXTEA variant, so that one decrypts with ChaCha too.
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

// Per-shader secrets (ShaderSecrets.ToDefines): a mask XORed into the key words and the ChaCha constants. Generated
// shaders define them before including this file, so they are compiled in rather than stored in the material.
#ifndef _SHELL_PROTECTOR_SECRETS
    #define _SHELL_PROTECTOR_KEY_MASK0 0u
    #define _SHELL_PROTECTOR_KEY_MASK1 0u
    #define _SHELL_PROTECTOR_KEY_MASK2 0u
    #define _SHELL_PROTECTOR_KEY_MASK3 0u
    #define _SHELL_PROTECTOR_CHACHA_C0 0x61707865u
    #define _SHELL_PROTECTOR_CHACHA_C1 0x3320646eu
    #define _SHELL_PROTECTOR_CHACHA_C2 0x79622d32u
    #define _SHELL_PROTECTOR_CHACHA_C3 0x6b206574u
#endif

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
		_SHELL_PROTECTOR_KEY_MASK0 ^ ((uint)round(_Key0) | ((uint)round(_Key1) << 8) | ((uint)round(_Key2) << 16) | ((uint)round(_Key3) << 24)),
		_SHELL_PROTECTOR_KEY_MASK1 ^ ((uint)round(_Key4) | ((uint)round(_Key5) << 8) | ((uint)round(_Key6) << 16) | ((uint)round(_Key7) << 24)),
		_SHELL_PROTECTOR_KEY_MASK2 ^ ((uint)round(_Key8) | ((uint)round(_Key9) << 8) | ((uint)round(_Key10) << 16) | ((uint)round(_Key11) << 24)),
		_SHELL_PROTECTOR_KEY_MASK3 ^ ((uint)round(_Key12) | ((uint)round(_Key13) << 8) | ((uint)round(_Key14) << 16) | ((uint)round(_Key15) << 24)) ^ (uint)((idx >> _SHELL_PROTECTOR_INDEX_ALIGNMENT) << _SHELL_PROTECTOR_INDEX_ALIGNMENT) ^ ((uint)m << 24)
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
    return GetPixel(tex0, tex1, tex0Sampler, data, uv, m);
}

#ifdef _SHELL_PROTECTOR_BC7

// The mip reference covers the full chain of the texture's own size (Pipeline.GetMipTexture), and BC7 keeps every level
// down to 1x1, so the level only has to stay below the texture's mip count.
int GetBC7Mip(Texture2D mipTex, SamplerState mipSamp, float2 uv)
{
	const int mip = round(mipTex.Sample(mipSamp, uv).r * 255 / 10);
	return clamp(mip, 0, (int)_ShellSourceSampling.x - 1);
}

float4 DecryptTextureBox(Texture2D tex0, Texture2D tex1, SamplerState texSampler, float4 texSize, Texture2D mipTex, SamplerState mipSamp, float2 uv)
{
	return DecryptTexture(tex0, tex1, texSampler, uv, GetBC7Mip(mipTex, mipSamp, uv));
}

// Taps on the texel centers of the selected mip level; texSize is the atlas's and unused. A decrypted record is a whole 4x4
// block, so the taps in a block already decrypted are decoded from it and only taps in another block decrypt again.
float4 DecryptTextureBilinear(Texture2D tex0, Texture2D tex1, SamplerState texSampler, float4 texSize, Texture2D mipTex, SamplerState mipSamp, float2 uv)
{
	const int mip = GetBC7Mip(mipTex, mipSamp, uv);
	const float2 size = BC7MipSize(mip);
	const float2 position = uv * size - 0.5;
	const float2 uv00 = (floor(position) + 0.5) / size;
	const float2 uv10 = uv00 + float2(1.0 / size.x, 0);
	const float2 uv01 = uv00 + float2(0, 1.0 / size.y);
	const float2 uv11 = uv00 + 1.0 / size;

	const int unit00 = GetBlockIndex(uv00, mip);
	const int unit10 = GetBlockIndex(uv10, mip);
	const int unit01 = GetBlockIndex(uv01, mip);
	const int unit11 = GetBlockIndex(uv11, mip);

	uint data[_SHELL_PROTECTOR_DATA_LENGTH];
	DecryptData(data, tex0, tex1, texSampler, uv00, mip);
	const float4 c00 = GetPixel(tex0, tex1, texSampler, data, uv00, mip);
	float4 c10 = 0;
	float4 c01 = 0;
	float4 c11 = 0;
	if (unit10 == unit00)
		c10 = GetPixel(tex0, tex1, texSampler, data, uv10, mip);
	if (unit01 == unit00)
		c01 = GetPixel(tex0, tex1, texSampler, data, uv01, mip);
	if (unit11 == unit00)
		c11 = GetPixel(tex0, tex1, texSampler, data, uv11, mip);

	[branch]
	if (unit10 != unit00)
	{
		DecryptData(data, tex0, tex1, texSampler, uv10, mip);
		c10 = GetPixel(tex0, tex1, texSampler, data, uv10, mip);
		if (unit11 == unit10)
			c11 = GetPixel(tex0, tex1, texSampler, data, uv11, mip);
	}
	[branch]
	if (unit01 != unit00)
	{
		DecryptData(data, tex0, tex1, texSampler, uv01, mip);
		c01 = GetPixel(tex0, tex1, texSampler, data, uv01, mip);
		if (unit11 == unit01)
			c11 = GetPixel(tex0, tex1, texSampler, data, uv11, mip);
	}
	[branch]
	if (unit11 != unit00 && unit11 != unit10 && unit11 != unit01)
	{
		DecryptData(data, tex0, tex1, texSampler, uv11, mip);
		c11 = GetPixel(tex0, tex1, texSampler, data, uv11, mip);
	}

	const float2 f = frac(position);
	return lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
}

#else

float4 DecryptTextureBox(Texture2D tex0, Texture2D tex1, SamplerState texSampler, float4 texSize, Texture2D mipTex, SamplerState mipSamp, float2 uv)
{
	float4 mipPixel = mipTex.Sample(mipSamp, uv);

	int mip = round(mipPixel.r * 255 / 10); //fucking precision problems
	const int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 }; // max size 4k

    float4 c00 = DecryptTexture(tex0, tex1, texSampler, uv, m[mip]);

	return c00;
}

float4 DecryptTextureBilinear(Texture2D tex0, Texture2D tex1, SamplerState texSampler, float4 originalTexSize, Texture2D mipTex, SamplerState mipSamp, float2 uv)
{
	const float4 mipPixel = mipTex.Sample(mipSamp, uv);
	const float2 uvUnit = originalTexSize.xy;
	//bilinear interpolation
	const float2 uvBilinear = uv - 0.5 * uvUnit;
	const int mip = round(mipPixel.r * 255 / 10); //fucking precision problems
	const int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 }; // max size 4k

	const float2 uv00 = uvBilinear + float2(uvUnit.x * 0, uvUnit.y * 0);
	const float2 uv10 = uvBilinear + float2(uvUnit.x * 1, uvUnit.y * 0);
	const float2 uv01 = uvBilinear + float2(uvUnit.x * 0, uvUnit.y * 1);
	const float2 uv11 = uvBilinear + float2(uvUnit.x * 1, uvUnit.y * 1);

#if defined(_SHELL_PROTECTOR_DXT) || defined(_SHELL_PROTECTOR_BLOCK_STREAM)
	{
		// One decryption covers a whole unit (ChaCha DXT: 4x4 blocks = 16x16 texels, ChaCha RGB/RGBA: 4x4 pixels,
		// XXTEA DXT: 2 blocks = 8x4 texels).
		// Decrypt again only for the taps that land in another unit, so a warp only pays for the crossings
		// its lanes actually hit (at most 4, same as decrypting every tap).
		const int unit00 = GetBlockIndex(uv00, m[mip]) >> _SHELL_PROTECTOR_INDEX_ALIGNMENT;
		const int unit10 = GetBlockIndex(uv10, m[mip]) >> _SHELL_PROTECTOR_INDEX_ALIGNMENT;
		const int unit01 = GetBlockIndex(uv01, m[mip]) >> _SHELL_PROTECTOR_INDEX_ALIGNMENT;
		const int unit11 = GetBlockIndex(uv11, m[mip]) >> _SHELL_PROTECTOR_INDEX_ALIGNMENT;
		const int k00 = GetBlockLocalIndex(uv00, m[mip]);
		const int k10 = GetBlockLocalIndex(uv10, m[mip]);
		const int k01 = GetBlockLocalIndex(uv01, m[mip]);
		const int k11 = GetBlockLocalIndex(uv11, m[mip]);

		uint data[_SHELL_PROTECTOR_DATA_LENGTH];
		DecryptData(data, tex0, tex1, texSampler, uv00, m[mip]);
		const uint word00 = SelectWord(data, k00);
		uint word10 = SelectWord(data, k10);
		uint word01 = SelectWord(data, k01);
		uint word11 = SelectWord(data, k11);

		[branch]
		if (unit10 != unit00)
		{
			DecryptData(data, tex0, tex1, texSampler, uv10, m[mip]);
			word10 = SelectWord(data, k10);
			if (unit11 == unit10)
				word11 = SelectWord(data, k11);
		}
		[branch]
		if (unit01 != unit00)
		{
			DecryptData(data, tex0, tex1, texSampler, uv01, m[mip]);
			word01 = SelectWord(data, k01);
			if (unit11 == unit01)
				word11 = SelectWord(data, k11);
		}
		[branch]
		if (unit11 != unit00 && unit11 != unit10 && unit11 != unit01)
		{
			DecryptData(data, tex0, tex1, texSampler, uv11, m[mip]);
			word11 = SelectWord(data, k11);
		}

		const float4 c00 = GetBlockPixel(tex0, tex1, texSampler, word00, uv00, m[mip]);
		const float4 c10 = GetBlockPixel(tex0, tex1, texSampler, word10, uv10, m[mip]);
		const float4 c01 = GetBlockPixel(tex0, tex1, texSampler, word01, uv01, m[mip]);
		const float4 c11 = GetBlockPixel(tex0, tex1, texSampler, word11, uv11, m[mip]);
		const float2 f = frac(uvBilinear * originalTexSize.zw);
		const float4 c0 = lerp(c00, c10, f.x);
		const float4 c1 = lerp(c01, c11, f.x);
		return lerp(c0, c1, f.y);
	}
#else
	float4 c00 = DecryptTexture(tex0, tex1, texSampler, uv00, m[mip]);
	float4 c10 = DecryptTexture(tex0, tex1, texSampler, uv10, m[mip]);
	float4 c01 = DecryptTexture(tex0, tex1, texSampler, uv01, m[mip]);
	float4 c11 = DecryptTexture(tex0, tex1, texSampler, uv11, m[mip]);

	float2 f = frac(uvBilinear * originalTexSize.zw);

	float4 c0 = lerp(c00, c10, f.x);
	float4 c1 = lerp(c01, c11, f.x);

	float4 bilinear = lerp(c0, c1, f.y);

	return bilinear;
#endif
}

#endif

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
