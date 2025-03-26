#pragma once

#define _SHELL_PROTECTOR_DATA_LENGTH 2
#define _SHELL_PROTECTOR_INDEX_ALIGNMENT 1

int GetIndex(half2 uv, int m) {
	half x = frac(uv.x);
	half y = frac(uv.y);

	return (mw[m + (uint)_Woffset] * floor(y * mh[m + _Hoffset])) + floor(x * mw[m + (uint)_Woffset]);
}

void GetData(inout uint data[2], half2 uv, int m) {
	int idx = GetIndex(uv, m);
	int offset = (idx & 1) == 0 ? 0 : -1;
	
	half4 pixels[2];
	pixels[0] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 0 + offset, m, (uint)_Woffset, _Hoffset), m);
	pixels[1] = _EncryptTex0.SampleLevel(sampler_EncryptTex0, GetUV(idx + 1 + offset, m, (uint)_Woffset, _Hoffset), m);

	data[0] = ((uint)round(pixels[0].r * 255.0f) | ((uint)round(pixels[0].g * 255.0f) << 8) | ((uint)round(pixels[0].b * 255.0f) << 16) | ((uint)round(pixels[0].a * 255.0f) << 24));
	data[1] = ((uint)round(pixels[1].r * 255.0f) | ((uint)round(pixels[1].g * 255.0f) << 8) | ((uint)round(pixels[1].b * 255.0f) << 16) | ((uint)round(pixels[1].a * 255.0f) << 24));
}

half4 GetPixel(inout uint data[2], half2 uv, int m) {
	int idx = GetIndex(uv, m);
	half r = ((data[idx & 1] & 0x000000FF) >>  0)/255.0f;
	half g = ((data[idx & 1] & 0x0000FF00) >>  8)/255.0f;
	half b = ((data[idx & 1] & 0x00FF0000) >> 16)/255.0f;
	half a = ((data[idx & 1] & 0xFF000000) >> 24)/255.0f;
	half4 decrypt = half4(r, g, b, a);

	return half4(GammaCorrection(decrypt.rgb), decrypt.a);
}