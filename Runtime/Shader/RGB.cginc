#pragma once

#define _SHELL_PROTECTOR_DATA_LENGTH 3
#define _SHELL_PROTECTOR_INDEX_ALIGNMENT 2

int GetIndex(half2 uv, int m) {
	half x = frac(uv.x);
	half y = frac(uv.y);

	return (mw[m + (uint)_Woffset] * floor(y * mh[m + _Hoffset])) + floor(x * mw[m + (uint)_Woffset]);
}

void GetData(inout uint data[3], half2 uv, int m) {
	const int pos[4] = { 0, -1, -2, -3 };
	int idx = GetIndex(uv, m);
	int offset = pos[idx % 4];

    half3 pixels[4];
	pixels[0] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 0 + offset, m, (uint)_Woffset, _Hoffset), m);
	pixels[1] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 1 + offset, m, (uint)_Woffset, _Hoffset), m);
	pixels[2] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 2 + offset, m, (uint)_Woffset, _Hoffset), m);
	pixels[3] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 3 + offset, m, (uint)_Woffset, _Hoffset), m);

	data[0] = ((uint)round(pixels[0].r * 255.0f) | ((uint)round(pixels[0].g * 255.0f) << 8) | ((uint)round(pixels[0].b * 255.0f) << 16) | ((uint)round(pixels[1].r * 255.0f) << 24));
	data[1] = ((uint)round(pixels[1].g * 255.0f) | ((uint)round(pixels[1].b * 255.0f) << 8) | ((uint)round(pixels[2].r * 255.0f) << 16) | ((uint)round(pixels[2].g * 255.0f) << 24));
	data[2] = ((uint)round(pixels[2].b * 255.0f) | ((uint)round(pixels[3].r * 255.0f) << 8) | ((uint)round(pixels[3].g * 255.0f) << 16) | ((uint)round(pixels[3].b * 255.0f) << 24));
}

half4 GetPixel(inout uint data[3], half2 uv, int m) {
    const int pos[4] = { 0, -1, -2, -3 };
	int idx = GetIndex(uv, m);
	int offset = pos[idx % 4];

	half r[4] = { (data[0] & 0x000000FF)/255.0f, ((data[0] & 0xFF000000) >> 24)/255.0f, ((data[1] & 0x00FF0000) >> 16)/255.0f, ((data[2] & 0x0000FF00) >> 8)/255.0f };
	half g[4] = { ((data[0] & 0x0000FF00) >> 8)/255.0f, ((data[1] & 0x000000FF) >> 0)/255.0f, ((data[1] & 0xFF000000) >> 24)/255.0f, ((data[2] & 0x00FF0000) >> 16)/255.0f };
	half b[4] = { ((data[0] & 0x00FF0000) >> 16)/255.0f, ((data[1] & 0x0000FF00) >> 8)/255.0f, ((data[2] & 0x000000FF) >> 0)/255.0f, ((data[2] & 0xFF000000) >> 24)/255.0f };
	half3 decrypt = half3(r[idx % 4], g[idx % 4], b[idx % 4]);

	return half4(GammaCorrection(decrypt), 1.0);
}