#pragma once

// ChaCha: one 64-byte keystream block covers a 4x4 pixel block, one keystream word per pixel.
// All bilinear taps inside the same block share a single keystream.
#define _SHELL_PROTECTOR_BLOCK_STREAM
#define _SHELL_PROTECTOR_DATA_LENGTH 16
#define _SHELL_PROTECTOR_INDEX_ALIGNMENT 0

int GetBlockIndex(float2 uv, int m)
{
	const uint2 p = GetPixelCoord(uv, m);
	const uint blocksPerRow = (mipDimensions[m + _Woffset] + 3) >> 2;
	return (p.y >> 2) * blocksPerRow + (p.x >> 2);
}

int GetBlockLocalIndex(float2 uv, int m)
{
	const uint2 p = GetPixelCoord(uv, m);
	return (p.y & 3) * 4 + (p.x & 3);
}

void GetData(Texture2D tex1, SamplerState tex0Sampler, inout uint data[16], float2 uv, int m)
{
	// Decrypt() XORs the keystream into this, leaving the raw keystream.
	[unroll]
	for (int i = 0; i < 16; ++i)
		data[i] = 0;
}

float4 GetBlockPixel(Texture2D tex0, SamplerState tex0Sampler, uint keystream, float2 uv, int m)
{
	const float2 size = float2(mipDimensions[m + _Woffset], mipDimensions[m + _Hoffset]);
	const float4 pixel = tex0.SampleLevel(tex0Sampler, (GetPixelCoord(uv, m) + 0.5) / size, m);

	uint data = ((uint)round(pixel.r * 255.0f) | ((uint)round(pixel.g * 255.0f) << 8) | ((uint)round(pixel.b * 255.0f) << 16) | ((uint)round(pixel.a * 255.0f) << 24));
	data ^= keystream;

	half r = ((data & 0x000000FF) >>  0)/255.0f;
	half g = ((data & 0x0000FF00) >>  8)/255.0f;
	half b = ((data & 0x00FF0000) >> 16)/255.0f;
	half a = ((data & 0xFF000000) >> 24)/255.0f;
	float4 decrypt = float4(r, g, b, a);

	#ifdef _SHELL_PROTECTOR_RGBA
	return float4(GammaCorrection(decrypt.rgb), decrypt.a);
#else
	return half4(GammaCorrection(decrypt.rgb), 1.0);
#endif
}

float4 GetPixel(Texture2D tex0, SamplerState tex0Sampler, in uint data[16], float2 uv, int m)
{
	return GetBlockPixel(tex0, tex0Sampler, data[GetBlockLocalIndex(uv, m)], uv, m);
}
