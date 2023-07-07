#ifndef Decrypt
#define Decrypt

static uint mw[13] = { 4096, 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };
static uint mh[13] = { 4096, 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };

static uint k[8] = { 0, 0, 0, 0, 0, 0, 0, 0 };

static const uint Delta = 0x9e3779b9;

float3 InverseGammaCorrection(float3 rgb)
{
	float3 result = pow(rgb, 0.454545);
	return result;
}
float3 GammaCorrection(float3 rgb)
{
	float3 result = pow(rgb, 2.2);
	return result;
}

void XXTEADecrypt(float3 pixel[4], out uint data[3])
{
	data[0] = ((uint)round(pixel[0].r * 255.0f) + ((uint)round(pixel[0].g * 255.0f) << 8) + ((uint)round(pixel[0].b * 255.0f) << 16) + ((uint)round(pixel[1].r * 255.0f) << 24));
	data[1] = ((uint)round(pixel[1].g * 255.0f) + ((uint)round(pixel[1].b * 255.0f) << 8) + ((uint)round(pixel[2].r * 255.0f) << 16) + ((uint)round(pixel[2].g * 255.0f) << 24));
	data[2] = ((uint)round(pixel[2].b * 255.0f) + ((uint)round(pixel[3].r * 255.0f) << 8) + ((uint)round(pixel[3].g * 255.0f) << 16) + ((uint)round(pixel[3].b * 255.0f) << 24));
	
	uint n = 3;
	uint v0, v1, sum;
	uint p, rounds, e;

	rounds = 6 + floor(52 / n);
	sum = rounds * Delta;

	v0 = data[0];
	do
	{
		e = (sum >> 2) & 3;
		for (p = n-1; p > 0; p--)
		{
			v1 = data[p - 1];
			data[p] -= (((v1 >> 5 ^ v0 << 2) + (v0 >> 3 ^ v1 << 4)) ^ ((sum ^ v0) + (k[(p & 3) ^ e] ^ v1)));
			v0 = data[p];
		}
		v1 = data[n - 1];
		data[0] -= (((v1 >> 5 ^ v0 << 2) + (v0 >> 3 ^ v1 << 4)) ^ ((sum ^ v0) + (k[(p & 3) ^ e] ^ v1)));
		v0 = data[0];
		sum -= Delta;
	} while (--rounds > 0);
}

void XXTEADecrypt(float4 pixel[2], out uint data[2])
{
	data[0] = ((uint)round(pixel[0].r * 255.0f) + ((uint)round(pixel[0].g * 255.0f) << 8) + ((uint)round(pixel[0].b * 255.0f) << 16) + ((uint)round(pixel[0].a * 255.0f) << 24));
	data[1] = ((uint)round(pixel[1].r * 255.0f) + ((uint)round(pixel[1].g * 255.0f) << 8) + ((uint)round(pixel[1].b * 255.0f) << 16) + ((uint)round(pixel[1].a * 255.0f) << 24));

	uint n = 2;
	uint v0, v1, sum;
	uint p, rounds, e;

	rounds = 6 + floor(52 / n);
	sum = rounds * Delta;

	v0 = data[0];
	do
	{
		e = (sum >> 2) & 3;
		for (p = n-1; p > 0; p--)
		{
			v1 = data[p - 1];
			data[p] -= (((v1 >> 5 ^ v0 << 2) + (v0 >> 3 ^ v1 << 4)) ^ ((sum ^ v0) + (k[(p & 3) ^ e] ^ v1)));
			v0 = data[p];
		}
		v1 = data[n - 1];
		data[0] -= (((v1 >> 5 ^ v0 << 2) + (v0 >> 3 ^ v1 << 4)) ^ ((sum ^ v0) + (k[(p & 3) ^ e] ^ v1)));
		v0 = data[0];
		sum -= Delta;
	} while (--rounds > 0);
}

float2 GetUV(int idx, int m, int woffset = 0, int hoffset = 0)
{
	int w = idx % mw[m + woffset];
	int h = idx / mw[m + hoffset];
	return float2((float)w/mw[m + woffset], (float)h/mh[m + hoffset]);
}


float4 DecryptTextureXXTEA(float2 uv, int m)
{
	float x = frac(uv.x);
	float y = frac(uv.y);

	int w = 13 - log2(_MainTex_TexelSize.z) - 1;
	int h = 13 - log2(_MainTex_TexelSize.w) - 1;
	int idx = (mw[m + w] * floor(y * mh[m + h])) + floor(x * mw[m + w]);
	k[3] = floor(idx / 4) * 4;
	
	float3 pixels[4];
	
	int pos[4] = { 0, -1, -2, -3 };
	int offset = pos[idx % 4];
	pixels[0] = _MainTex.SampleLevel(sampler_MainTex, GetUV(idx + 0 + offset, m, w, h), m);
	pixels[1] = _MainTex.SampleLevel(sampler_MainTex, GetUV(idx + 1 + offset, m, w, h), m);
	pixels[2] = _MainTex.SampleLevel(sampler_MainTex, GetUV(idx + 2 + offset, m, w, h), m);
	pixels[3] = _MainTex.SampleLevel(sampler_MainTex, GetUV(idx + 3 + offset, m, w, h), m);

	uint data[3] = { 0, 0, 0 };
	data[0] = (round(pixels[0].r * 255.0f) + ((uint)round(pixels[0].g * 255.0f) << 8) + ((uint)round(pixels[0].b * 255.0f) << 16) + ((uint)round(pixels[1].r * 255.0f) << 24));
	data[1] = (round(pixels[1].g * 255.0f) + ((uint)round(pixels[1].b * 255.0f) << 8) + ((uint)round(pixels[2].r * 255.0f) << 16) + ((uint)round(pixels[2].g * 255.0f) << 24));
	data[2] = (round(pixels[2].b * 255.0f) + ((uint)round(pixels[3].r * 255.0f) << 8) + ((uint)round(pixels[3].g * 255.0f) << 16) + ((uint)round(pixels[3].b * 255.0f) << 24));

	XXTEADecrypt(pixels, data);

	float r[4] = { (data[0] & 0x000000FF)/255.0f, ((data[0] & 0xFF000000) >> 24)/255.0f, ((data[1] & 0x00FF0000) >> 16)/255.0f, ((data[2] & 0x0000FF00) >> 8)/255.0f };
	float g[4] = { ((data[0] & 0x0000FF00) >> 8)/255.0f, ((data[1] & 0x000000FF) >> 0)/255.0f, ((data[1] & 0xFF000000) >> 24)/255.0f, ((data[2] & 0x00FF0000) >> 16)/255.0f };
	float b[4] = { ((data[0] & 0x00FF0000) >> 16)/255.0f, ((data[1] & 0x0000FF00) >> 8)/255.0f, ((data[2] & 0x000000FF) >> 0)/255.0f, ((data[2] & 0xFF000000) >> 24)/255.0f };
	float3 decrypt = float3(r[idx % 4], g[idx % 4], b[idx % 4]);

	return float4(GammaCorrection(decrypt), 1.0);
}
float4 DecryptTextureXXTEARGBA(float2 uv, int m)
{
	float x = frac(uv.x);
	float y = frac(uv.y);

	int w = 13 - log2(_MainTex_TexelSize.z) - 1;
	int h = 13 - log2(_MainTex_TexelSize.w) - 1;
	int idx = (mw[m + w] * floor(y * mh[m + h])) + floor(x * mw[m + w]);
	k[3] = floor(idx / 2) * 2;
	
	float4 pixels[2];
	
	int pos[2] = { 0, -1 };
	int offset = pos[idx % 2];
	pixels[0] = _MainTex.SampleLevel(sampler_MainTex, GetUV(idx + 0 + offset, m, w, h), m);
	pixels[1] = _MainTex.SampleLevel(sampler_MainTex, GetUV(idx + 1 + offset, m, w, h), m);

	uint data[2] = { 0, 0 };
	data[0] = (round(pixels[0].r * 255.0f) + ((uint)round(pixels[0].g * 255.0f) << 8) + ((uint)round(pixels[0].b * 255.0f) << 16) + ((uint)round(pixels[0].a * 255.0f) << 24));
	data[1] = (round(pixels[1].r * 255.0f) + ((uint)round(pixels[1].g * 255.0f) << 8) + ((uint)round(pixels[1].b * 255.0f) << 16) + ((uint)round(pixels[1].a * 255.0f) << 24));

	XXTEADecrypt(pixels, data);

	float r[2] = { (data[0] & 0x000000FF)/255.0f, ((data[1] & 0x000000FF))/255.0f };
	float g[2] = { ((data[0] & 0x0000FF00) >>  8)/255.0f, ((data[1] & 0x0000FF00) >>  8)/255.0f };
	float b[2] = { ((data[0] & 0x00FF0000) >> 16)/255.0f, ((data[1] & 0x00FF0000) >> 16)/255.0f };
	float a[2] = { ((data[0] & 0xFF000000) >> 24)/255.0f, ((data[1] & 0xFF000000) >> 24)/255.0f };
	float4 decrypt = float4(r[idx % 2], g[idx % 2], b[idx % 2], a[idx % 2]);

	return float4(GammaCorrection(decrypt.rgb), decrypt.a);
}
float4 DecryptTextureXXTEADXT1(float2 uv, int m)
{
	float x = frac(uv.x);
	float y = frac(uv.y);

	uint woffset = 13 - log2(_MainTex_TexelSize.z) - 1 + 2;
	uint hoffset = 13 - log2(_MainTex_TexelSize.w) - 1 + 2;
	
	float4 col = _MainTex.SampleLevel(sampler_MainTex, uv, m);
	
	uint w = mw[m + woffset];
	uint h = mh[m + hoffset];
	
	int idx = (w * floor(y * h)) + floor(x * w);
	
	k[3] = floor(idx / 2) * 2;
	
	int pos[2] = { 0, -1 };
	int offset = pos[idx % 2];
	
	float4 pixels[2];
	pixels[0] = _EncryptTex.SampleLevel(sampler_MainTex, GetUV(idx + 0 + offset, m, woffset, hoffset), m);
	pixels[1] = _EncryptTex.SampleLevel(sampler_MainTex, GetUV(idx + 1 + offset, m, woffset, hoffset), m);
	
	uint data[2];
	data[0] = (round(pixels[0].r * 255.0f) + ((uint)round(pixels[0].g * 255.0f) << 8) + ((uint)round(pixels[0].b * 255.0f) << 16) + ((uint)round(pixels[0].a * 255.0f) << 24));
	data[1] = (round(pixels[1].r * 255.0f) + ((uint)round(pixels[1].g * 255.0f) << 8) + ((uint)round(pixels[1].b * 255.0f) << 16) + ((uint)round(pixels[1].a * 255.0f) << 24));
	
	XXTEADecrypt(pixels, data);
	
	uint r[2] = { (data[0] & 0x000000FF), ((data[1] & 0x000000FF)) };
	uint g[2] = { ((data[0] & 0x0000FF00) >>  8), ((data[1] & 0x0000FF00) >>  8) };
	uint b[2] = { ((data[0] & 0x00FF0000) >> 16), ((data[1] & 0x00FF0000) >> 16) };
	uint a[2] = { ((data[0] & 0xFF000000) >> 24), ((data[1] & 0xFF000000) >> 24) };
	
	uint color1 = (r[idx % 2] | g[idx % 2] << 8);
	uint color2 = (b[idx % 2] | a[idx % 2] << 8);
	
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
	
	float3 col1 = float3(color1_r / 255.0f, color1_g / 255.0f, color1_b / 255.0f);
	float3 col2 = float3(color2_r / 255.0f, color2_g / 255.0f, color2_b / 255.0f);
	
	float3 result;
	if(color1 > color2)
		result = lerp(col2, col1, col);
	else
		result = lerp(col2, col1, 0.5);
	return float4(GammaCorrection(result), 1);
}
#endif
