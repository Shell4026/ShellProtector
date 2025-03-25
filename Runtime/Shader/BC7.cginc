#pragma once

#define _SHELL_PROTECTOR_DATA_LENGTH 2
#define _SHELL_PROTECTOR_INDEX_ALIGNMENT 1

int GetIndex(half2 uv, int m) {
    half x = frac(uv.x);
    half y = frac(uv.y);

    uint w = mw[m + (uint)_Woffset];
    uint h = mh[m + _Hoffset];

    return (w * floor(y * h)) + floor(x * w);
}

void GetData(inout uint data[2], half2 uv, int m) {
    int idx = GetIndex(uv, m);
    int offset = (idx & 1) == 0 ? 0 : -1;

    half4 pixels[2];
    pixels[0] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 0 + offset, m, (uint)_Woffset, _Hoffset), m);
    pixels[1] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 1 + offset, m, (uint)_Woffset, _Hoffset), m);

    data[0] = ((uint)round(pixels[0].r * 255.0f) | ((uint)round(pixels[0].g * 255.0f) << 8) | ((uint)round(pixels[0].b * 255.0f) << 16) | ((uint)round(pixels[0].a * 255.0f) << 24));
    data[1] = ((uint)round(pixels[1].r * 255.0f) | ((uint)round(pixels[1].g * 255.0f) << 8) | ((uint)round(pixels[1].b * 255.0f) << 16) | ((uint)round(pixels[1].a * 255.0f) << 24));
}

// BC7 블록 모드별 색상 보간 함수
half3 InterpolateBC7Colors(half3 endpoint0, half3 endpoint1, uint weight, uint totalWeights) {
    if (totalWeights == 4) {
        switch(weight) {
            case 0: return endpoint0;
            case 1: return lerp(endpoint0, endpoint1, 0.333333);
            case 2: return lerp(endpoint0, endpoint1, 0.666667);
            case 3: return endpoint1;
        }
    }
    else if (totalWeights == 8) {
        return lerp(endpoint0, endpoint1, weight / 7.0);
    }
    return endpoint0;
}

half4 GetPixel(inout uint data[2], half2 uv, int m) {
    int idx = GetIndex(uv, m);
    half4 col = _EncryptTex0.SampleLevel(sampler_EncryptTex0, uv, m);
    
    // BC7 데이터 디코딩
    uint endpoint0 = data[0];
    uint endpoint1 = data[1];
    
    half3 color0, color1;
    color0.r = (endpoint0 & 0xFF) / 255.0;
    color0.g = ((endpoint0 >> 8) & 0xFF) / 255.0;
    color0.b = ((endpoint0 >> 16) & 0xFF) / 255.0;
    
    color1.r = (endpoint1 & 0xFF) / 255.0;
    color1.g = ((endpoint1 >> 8) & 0xFF) / 255.0;
    color1.b = ((endpoint1 >> 16) & 0xFF) / 255.0;
    
    half alpha0 = ((endpoint0 >> 24) & 0xFF) / 255.0;
    half alpha1 = ((endpoint1 >> 24) & 0xFF) / 255.0;
    
    // BC7 보간 가중치 계산
    half weight = col.r;
    half3 finalColor = lerp(color0, color1, weight);
    half finalAlpha = lerp(alpha0, alpha1, col.a);
    
    return half4(GammaCorrection(finalColor), finalAlpha);
} 