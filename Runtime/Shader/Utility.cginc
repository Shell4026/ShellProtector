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

// Shared by main-map and emission DXT decoders. Packed RGB565 endpoints
// are decrypted separately from the compressed texture's palette selectors.
float3 ShellRGB565(uint color)
{
    uint r = (color >> 11) & 31u;
    uint g = (color >> 5) & 63u;
    uint b = color & 31u;
    return float3((r << 3) | (r >> 2), (g << 2) | (g >> 4), (b << 3) | (b >> 2)) / 255.0;
}

float3 ShellDXTColors(uint endpoints, float3 selector)
{
    return lerp(ShellRGB565(endpoints >> 16), ShellRGB565(endpoints & 65535u), selector);
}

float2 GetUV(int idx, int m, int woffset = 0, int hoffset = 0)
{
	int w = idx % mw[m + woffset];
	int h = idx / mw[m + woffset];
	return float2((float)w/mw[m + woffset], (float)h/mh[m + hoffset]);
}

uint2 GetPixelCoord(float2 uv, int m)
{
	return (uint2)floor(frac(uv) * float2(mw[m + _Woffset], mh[m + _Hoffset]));
}

// data[k] with constant indices only. A per-pixel k into a local array becomes an indexable temp,
// which AMD compiles to a loop over every distinct k in the wave (verified with RGA on GCN and RDNA).
uint SelectWord(const uint data[16], int k)
{
	const bool b0 = (k & 1) != 0;
	const bool b1 = (k & 2) != 0;
	const bool b2 = (k & 4) != 0;
	const bool b3 = (k & 8) != 0;
	// Each step halves the candidates by one bit of k: a, b = data[2j + b0] for j < 8, c = data[4j + 2*b1 + b0], ...
	const uint4 a = b0 ? uint4(data[1], data[3], data[5], data[7]) : uint4(data[0], data[2], data[4], data[6]);
	const uint4 b = b0 ? uint4(data[9], data[11], data[13], data[15]) : uint4(data[8], data[10], data[12], data[14]);
	const uint4 c = b1 ? uint4(a.yw, b.yw) : uint4(a.xz, b.xz);
	const uint2 d = b2 ? c.yw : c.xz;
	return b3 ? d.y : d.x;
}

// A BC7 record (BC7.cginc). Only the low three bits of k are used.
uint SelectWord(const uint data[8], int k)
{
	const bool b0 = (k & 1) != 0;
	const bool b1 = (k & 2) != 0;
	const bool b2 = (k & 4) != 0;
	const uint4 a = b0 ? uint4(data[1], data[3], data[5], data[7]) : uint4(data[0], data[2], data[4], data[6]);
	const uint2 c = b1 ? a.yw : a.xz;
	return b2 ? c.y : c.x;
}

uint SelectWord(const uint data[2], int k)
{
	return (k & 1) != 0 ? data[1] : data[0];
}
