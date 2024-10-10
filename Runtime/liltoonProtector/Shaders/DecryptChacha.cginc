#pragma once
#include "Chacha.cginc"

static const uint mw[13] = { 4096, 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };
static const uint mh[13] = { 4096, 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };

float _Key0, _Key1, _Key2, _Key3, _Key4, _Key5, _Key6, _Key7, _Key8, _Key9, _Key10, _Key11, _Key12, _Key13, _Key14, _Key15;

uint _Woffset;
uint _Hoffset;

half3 InverseGammaCorrection(half3 rgb)
{
	half3 result = pow(rgb, 0.454545);
	return result;
}
half3 GammaCorrection(half3 rgb)
{
	//half3 result = pow(rgb, 2.2);
	half3 result = rgb * rgb * (rgb * (half)0.2 + (half)0.8); //fast pow
	return result;
}

half2 GetUV(int idx, int m, int woffset = 0, int hoffset = 0)
{
	int w = idx % mw[m + woffset];
	int h = idx / mw[m + woffset];
	return half2((half)w/mw[m + woffset], (half)h/mh[m + hoffset]);
}

half4 DecryptTexture(half2 uv, int m)
{
	half x = frac(uv.x);
	half y = frac(uv.y);

	int idx = (mw[m + (uint)_Woffset] * floor(y * mh[m + _Hoffset])) + floor(x * mw[m + (uint)_Woffset]);
	
	const uint key[4] = 
	{
		((uint)round(_Key0) | ((uint)round(_Key1) << 8) | ((uint)round(_Key2) << 16) | ((uint)round(_Key3) << 24)),
		((uint)round(_Key4) | ((uint)round(_Key5) << 8) | ((uint)round(_Key6) << 16) | ((uint)round(_Key7) << 24)),
		((uint)round(_Key8) | ((uint)round(_Key9) << 8) | ((uint)round(_Key10) << 16) | ((uint)round(_Key11) << 24)),
		((uint)round(_Key12) | ((uint)round(_Key13) << 8) | ((uint)round(_Key14) << 16) | ((uint)round(_Key15) << 24)) ^ (uint)((idx >> 2) << 2)
	};
	
	half3 pixels[4];
	
	const int pos[4] = { 0, -1, -2, -3 };
	int offset = pos[idx % 4];
	pixels[0] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 0 + offset, m, (uint)_Woffset, _Hoffset), m);
	pixels[1] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 1 + offset, m, (uint)_Woffset, _Hoffset), m);
	pixels[2] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 2 + offset, m, (uint)_Woffset, _Hoffset), m);
	pixels[3] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 3 + offset, m, (uint)_Woffset, _Hoffset), m);

	uint data[3] = { 0, 0, 0 };
	data[0] = ((uint)round(pixels[0].r * 255.0f) | ((uint)round(pixels[0].g * 255.0f) << 8) | ((uint)round(pixels[0].b * 255.0f) << 16) | ((uint)round(pixels[1].r * 255.0f) << 24));
	data[1] = ((uint)round(pixels[1].g * 255.0f) | ((uint)round(pixels[1].b * 255.0f) << 8) | ((uint)round(pixels[2].r * 255.0f) << 16) | ((uint)round(pixels[2].g * 255.0f) << 24));
	data[2] = ((uint)round(pixels[2].b * 255.0f) | ((uint)round(pixels[3].r * 255.0f) << 8) | ((uint)round(pixels[3].g * 255.0f) << 16) | ((uint)round(pixels[3].b * 255.0f) << 24));

	Chacha20XOR(data, key);

	half r[4] = { (data[0] & 0x000000FF)/255.0f, ((data[0] & 0xFF000000) >> 24)/255.0f, ((data[1] & 0x00FF0000) >> 16)/255.0f, ((data[2] & 0x0000FF00) >> 8)/255.0f };
	half g[4] = { ((data[0] & 0x0000FF00) >> 8)/255.0f, ((data[1] & 0x000000FF) >> 0)/255.0f, ((data[1] & 0xFF000000) >> 24)/255.0f, ((data[2] & 0x00FF0000) >> 16)/255.0f };
	half b[4] = { ((data[0] & 0x00FF0000) >> 16)/255.0f, ((data[1] & 0x0000FF00) >> 8)/255.0f, ((data[2] & 0x000000FF) >> 0)/255.0f, ((data[2] & 0xFF000000) >> 24)/255.0f };
	half3 decrypt = half3(r[idx % 4], g[idx % 4], b[idx % 4]);

	return half4(GammaCorrection(decrypt), 1.0);
}
half4 DecryptTextureRGBA(half2 uv, int m)
{
	half x = frac(uv.x);
	half y = frac(uv.y);

	int idx = (mw[m + (uint)_Woffset] * floor(y * mh[m + _Hoffset])) + floor(x * mw[m + (uint)_Woffset]);
	
	const uint key[4] = 
	{
		((uint)round(_Key0) | ((uint)round(_Key1) << 8) | ((uint)round(_Key2) << 16) | ((uint)round(_Key3) << 24)),
		((uint)round(_Key4) | ((uint)round(_Key5) << 8) | ((uint)round(_Key6) << 16) | ((uint)round(_Key7) << 24)),
		((uint)round(_Key8) | ((uint)round(_Key9) << 8) | ((uint)round(_Key10) << 16) | ((uint)round(_Key11) << 24)),
		((uint)round(_Key12) | ((uint)round(_Key13) << 8) | ((uint)round(_Key14) << 16) | ((uint)round(_Key15) << 24)) ^ (uint)((idx >> 1) << 1)
	};
	
	half4 pixels[2];
	
	int offset = (idx & 1) == 0 ? 0 : -1;
	pixels[0] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 0 + offset, m, (uint)_Woffset, _Hoffset), m);
	pixels[1] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 1 + offset, m, (uint)_Woffset, _Hoffset), m);

	uint data[2] = { 0, 0 };
	data[0] = ((uint)round(pixels[0].r * 255.0f) | ((uint)round(pixels[0].g * 255.0f) << 8) | ((uint)round(pixels[0].b * 255.0f) << 16) | ((uint)round(pixels[0].a * 255.0f) << 24));
	data[1] = ((uint)round(pixels[1].r * 255.0f) | ((uint)round(pixels[1].g * 255.0f) << 8) | ((uint)round(pixels[1].b * 255.0f) << 16) | ((uint)round(pixels[1].a * 255.0f) << 24));

	Chacha20XOR(data, key);

	half r = ((data[idx & 1] & 0x000000FF) >>  0)/255.0f;
	half g = ((data[idx & 1] & 0x0000FF00) >>  8)/255.0f;
	half b = ((data[idx & 1] & 0x00FF0000) >> 16)/255.0f;
	half a = ((data[idx & 1] & 0xFF000000) >> 24)/255.0f;
	half4 decrypt = half4(r, g, b, a);

	return half4(GammaCorrection(decrypt.rgb), decrypt.a);
}
half4 DecryptTextureDXT(half2 uv, int m)
{
	half2 frac_uv = frac(uv.xy);
	half x = frac_uv.x;
	half y = frac_uv.y;

	int miplv = 0;
	
	half4 col = _EncryptTex0.SampleLevel(sampler_EncryptTex0, uv, m);
	
	uint w = mw[m + (uint)_Woffset];
	uint h = mh[m + _Hoffset];
	
	int idx = (w * floor(y * h)) + floor(x * w);
	
	const uint key[4] = 
	{
		((uint)round(_Key0) | ((uint)round(_Key1) << 8) | ((uint)round(_Key2) << 16) | ((uint)round(_Key3) << 24)),
		((uint)round(_Key4) | ((uint)round(_Key5) << 8) | ((uint)round(_Key6) << 16) | ((uint)round(_Key7) << 24)),
		((uint)round(_Key8) | ((uint)round(_Key9) << 8) | ((uint)round(_Key10) << 16) | ((uint)round(_Key11) << 24)),
		((uint)round(_Key12) | ((uint)round(_Key13) << 8) | ((uint)round(_Key14) << 16) | ((uint)round(_Key15) << 24)) ^ (uint)((idx >> 1) << 1)
	};
	
	int offset = (idx & 1) == 0 ? 0 : -1;
	
	half4 pixels[2];
	pixels[0] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 0 + offset, m, (uint)_Woffset, _Hoffset), m);
	pixels[1] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 1 + offset, m, (uint)_Woffset, _Hoffset), m);
	
	uint data[2];
	data[0] = ((uint)round(pixels[0].r * 255.0f) | ((uint)round(pixels[0].g * 255.0f) << 8) | ((uint)round(pixels[0].b * 255.0f) << 16) | ((uint)round(pixels[0].a * 255.0f) << 24));
	data[1] = ((uint)round(pixels[1].r * 255.0f) | ((uint)round(pixels[1].g * 255.0f) << 8) | ((uint)round(pixels[1].b * 255.0f) << 16) | ((uint)round(pixels[1].a * 255.0f) << 24));
	
	Chacha20XOR(data, key);
	
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
	
	half3 col1 = half3(color1_r / (half)255, color1_g / (half)255, color1_b / (half)255);
	half3 col2 = half3(color2_r / (half)255, color2_g / (half)255, color2_b / (half)255);
	
	half3 result;
	result = lerp(col2, col1, color1 > color2 ? col.rgb : 0.5);
		
	return half4(GammaCorrection(result), col.a);
}