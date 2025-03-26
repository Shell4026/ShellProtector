#pragma once

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
