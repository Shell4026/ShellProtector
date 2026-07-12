#pragma once

#define _SHELL_PROTECTOR_DATA_LENGTH 2
#define _SHELL_PROTECTOR_INDEX_ALIGNMENT 1

int GetBlockIndex(float2 uv, int m) 
{
	const float x = frac(uv.x);
	const float y = frac(uv.y);

	const uint encW = mw[m + _Woffset];
	const uint encH = mh[m + _Hoffset];

	return (encW * floor(y * encH)) + floor(x * encW);
}

void GetData(Texture2D tex1, SamplerState tex0Sampler, inout uint data[2], float2 uv, int m) 
{
	int idx = GetBlockIndex(uv, m);
	int offset = (idx & 1) == 0 ? 0 : -1;

	float4 pixels[2];
    pixels[0] = tex1.SampleLevel(tex0Sampler, GetUV(idx + 0 + offset, m, _Woffset, _Hoffset), m);
    pixels[1] = tex1.SampleLevel(tex0Sampler, GetUV(idx + 1 + offset, m, _Woffset, _Hoffset), m);

	data[0] = ((uint)round(pixels[0].r * 255.0) | ((uint)round(pixels[0].g * 255.0) << 8) | ((uint)round(pixels[0].b * 255.0) << 16) | ((uint)round(pixels[0].a * 255.0) << 24));
	data[1] = ((uint)round(pixels[1].r * 255.0) | ((uint)round(pixels[1].g * 255.0) << 8) | ((uint)round(pixels[1].b * 255.0) << 16) | ((uint)round(pixels[1].a * 255.0) << 24));
}

float4 GetPixel(Texture2D tex0, SamplerState tex0Sampler, in uint data[2], float2 uv, int m) 
{
	int idx = GetBlockIndex(uv, m);
	int offset = (idx & 1) == 0 ? 0 : -1;

    float4 col = tex0.SampleLevel(tex0Sampler, uv, m);
	uint r = (data[idx & 1] & 0x000000FF) >> 0;
	uint g = (data[idx & 1] & 0x0000FF00) >> 8;
	uint b = (data[idx & 1] & 0x00FF0000) >> 16;
	uint a = (data[idx & 1] & 0xFF000000) >> 24;
	
	uint color1 = (r | g << 8);
	uint color2 = (b | a << 8);
	
	uint color1_r = (color1 & 0xF800) >> 11;
	color1_r = color1_r << 3 | color1_r >> 2;
	uint color1_g = (color1 & 0x7E0) >> 5;
	color1_g = color1_g << 2 | color1_g >> 4;
	uint color1_b= color1 & 0x1F;
	color1_b = color1_b << 3 | color1_b >> 2;
	
	uint color2_r = (color2 & 0xF800) >> 11;
	color2_r = color2_r << 3 | color2_r >> 2;
	uint color2_g = (color2 & 0x7E0) >> 5;
	color2_g = color2_g << 2 | color2_g >> 4;
	uint color2_b= color2 & 0x1F;
	color2_b = color2_b << 3 | color2_b >> 2;
	
	float3 col1 = float3(color1_r / 255.0, color1_g / 255.0, color1_b / 255.0);
	float3 col2 = float3(color2_r / 255.0, color2_g / 255.0, color2_b / 255.0);
	
	float3 result;
	result = lerp(col2, col1, color1 > color2 ? col.rgb : 0.5);

	return half4(GammaCorrection(result), col.a);
}