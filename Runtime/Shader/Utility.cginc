#pragma once

float3 InverseGammaCorrection(float3 rgb)
{
	float3 result = pow(rgb, 0.454545);
	return result;
}

float3 GammaCorrection(float3 rgb)
{
	//half3 result = pow(rgb, 2.2);
	float3 result = rgb * rgb * (rgb * (half)0.2 + (half)0.8); //fast pow
	return result;
}

float2 GetUV(int idx, int m, int woffset = 0, int hoffset = 0)
{
	int w = idx % mw[m + woffset];
	int h = idx / mw[m + woffset];
	return float2((float)w/mw[m + woffset], (float)h/mh[m + hoffset]);
}
