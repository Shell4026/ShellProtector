#pragma once

#pragma shader_feature_local _SHELL_PROTECTOR_XXTEA
#pragma shader_feature_local _SHELL_PROTECTOR_CHACHA
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT0
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT1
#pragma shader_feature_local _SHELL_PROTECTOR_RIMLIGHT

// Format keywords
#if !_SHELL_PROTECTOR_FORMAT0 && !_SHELL_PROTECTOR_FORMAT1
    #define _SHELL_PROTECTOR_DXT
#elif !_SHELL_PROTECTOR_FORMAT0 && _SHELL_PROTECTOR_FORMAT1
    #define _SHELL_PROTECTOR_RGBA
#elif _SHELL_PROTECTOR_FORMAT0 && !_SHELL_PROTECTOR_FORMAT1
    #define _SHELL_PROTECTOR_RGB
#else
    #error "Unsupported format"
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

half4 DecryptTexture(Texture2D tex0, Texture2D tex1, SamplerState tex0Sampler, half2 uv, int m)
{
	int idx = GetIndex(uv, m);
	const uint key[4] = 
	{
		((uint)round(_Key0) | ((uint)round(_Key1) << 8) | ((uint)round(_Key2) << 16) | ((uint)round(_Key3) << 24)),
		((uint)round(_Key4) | ((uint)round(_Key5) << 8) | ((uint)round(_Key6) << 16) | ((uint)round(_Key7) << 24)),
		((uint)round(_Key8) | ((uint)round(_Key9) << 8) | ((uint)round(_Key10) << 16) | ((uint)round(_Key11) << 24)),
		((uint)round(_Key12) | ((uint)round(_Key13) << 8) | ((uint)round(_Key14) << 16) | ((uint)round(_Key15) << 24)) ^ (uint)((idx >> _SHELL_PROTECTOR_INDEX_ALIGNMENT) << _SHELL_PROTECTOR_INDEX_ALIGNMENT)
	};
	
	uint data[_SHELL_PROTECTOR_DATA_LENGTH];
    GetData(tex1, tex0Sampler, data, uv, m);
	Decrypt(data, key);
    return GetPixel(tex0, tex0Sampler, data, uv, m);
}

half4 DecryptTextureBox(Texture2D tex0, Texture2D tex1, SamplerState texSampler, half4 texSize, Texture2D mipTex, SamplerState mipSamp, half2 uv)
{
	half4 mipPixel = mipTex.Sample(mipSamp, uv);

	int mip = round(mipPixel.r * 255 / 10); //fucking precision problems
	const int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 }; // max size 4k

    half4 c00 = DecryptTexture(tex0, tex1, texSampler, uv, m[mip]);

	return c00;
}

half4 DecryptTextureBilinear(Texture2D tex0, Texture2D tex1, SamplerState texSampler, half4 texSize, Texture2D mipTex, SamplerState mipSamp, half2 uv)
{
	const half4 mipPixel = mipTex.Sample(mipSamp, uv);
	const half2 uvUnit = texSize.xy;
	//bilinear interpolation
	const half2 uvBilinear = uv - 0.5 * uvUnit;
	const int mip = round(mipPixel.r * 255 / 10); //fucking precision problems
	const int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 }; // max size 4k

	half4 c00 = DecryptTexture(tex0, tex1, texSampler, uvBilinear + half2(uvUnit.x * 0, uvUnit.y * 0), m[mip]);
	half4 c10 = DecryptTexture(tex0, tex1, texSampler, uvBilinear + half2(uvUnit.x * 1, uvUnit.y * 0), m[mip]);
	half4 c01 = DecryptTexture(tex0, tex1, texSampler, uvBilinear + half2(uvUnit.x * 0, uvUnit.y * 1), m[mip]);
	half4 c11 = DecryptTexture(tex0, tex1, texSampler, uvBilinear + half2(uvUnit.x * 1, uvUnit.y * 1), m[mip]);

	half2 f = frac(uvBilinear * texSize.zw);

	half4 c0 = lerp(c00, c10, f.x);
	half4 c1 = lerp(c01, c11, f.x);

	half4 bilinear = lerp(c0, c1, f.y);

	return bilinear;
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