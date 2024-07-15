#ifndef Decrypt
#define Decrypt

static const uint mw[13] = { 4096, 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };
static const uint mh[13] = { 4096, 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };
static const uint Delta = 0x9e3779b9;

static const uint k[4] = { 0, 0, 0, 0 };
static const uint ROUNDS = 6;
static const uint CHACHA20_ROUNDS = 8;
static const uint SUM = Delta * ROUNDS;

static const uint WOFFSET = 0;
static const uint HOFFSET = 0;

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

void XXTEADecrypt(inout uint data[3], uint key[4])
{
	static const uint n = 3;
	uint v0, v1, sum;
	uint p, e;
	
	sum = SUM;

	v0 = data[0];
	for(int i = 0; i < ROUNDS; ++i)
	{
		e = (sum >> 2) & 3;
		for (p = n-1; p > 0; p--)
		{
			v1 = data[p - 1];
			data[p] -= (((v1 >> 5 ^ v0 << 2) + (v0 >> 3 ^ v1 << 4)) ^ ((sum ^ v0) + (key[(p & 3) ^ e] ^ v1)));
			v0 = data[p];
		}
		v1 = data[n - 1];
		data[0] -= (((v1 >> 5 ^ v0 << 2) + (v0 >> 3 ^ v1 << 4)) ^ ((sum ^ v0) + (key[(p & 3) ^ e] ^ v1)));
		v0 = data[0];
		sum -= Delta;
	}
}
void XXTEADecrypt(inout uint data[2], uint key[4])
{
	static const uint n = 2;
	uint v0, v1, sum;
	uint p, e;

	sum = SUM;

	v0 = data[0];
	for(int i = 0; i < ROUNDS; ++i)
	{
		e = (sum >> 2) & 3;
		for (p = n-1; p > 0; p--)
		{
			v1 = data[p - 1];
			data[p] -= (((v1 >> 5 ^ v0 << 2) + (v0 >> 3 ^ v1 << 4)) ^ ((sum ^ v0) + (key[(p & 3) ^ e] ^ v1)));
			v0 = data[p];
		}
		v1 = data[n - 1];
		data[0] -= (((v1 >> 5 ^ v0 << 2) + (v0 >> 3 ^ v1 << 4)) ^ ((sum ^ v0) + (key[(p & 3) ^ e] ^ v1)));
		v0 = data[0];
		sum -= Delta;
	}	
}

half2 GetUV(int idx, int m, int woffset = 0, int hoffset = 0)
{
	int w = idx % mw[m + woffset];
	int h = idx / mw[m + woffset];
	return half2((half)w/mw[m + woffset], (half)h/mh[m + hoffset]);
}

uint Rotl32(uint x, int n)
{
    return x << n | (x >> (32 - n));
}

void Chacha20QuarterRound(inout uint state[16], int a, int b, int c, int d)
{
	state[a] += state[b]; state[d] = Rotl32(state[d] ^ state[a], 16);
	state[c] += state[d]; state[b] = Rotl32(state[b] ^ state[c], 12);
	state[a] += state[b]; state[d] = Rotl32(state[d] ^ state[a], 8);
	state[c] += state[d]; state[b] = Rotl32(state[b] ^ state[c], 7);
}

void Chacha20XOR(inout uint data[2], uint key[4])
{
	uint4 state[4];
	state[0] = uint4(0x61707865, 0x3320646e, 0x79622d32, 0x6b206574);
	state[1] = uint4(key[0], key[1], key[2], key[3]);
	state[2] = state[1];
	state[3] = uint4(1, 0, 0, 0);

	uint block[16];
	//block
	[unroll]
	for(int i = 0; i < 4; ++i)
	{
		block[i * 4 + 0] = state[i].x;
		block[i * 4 + 1] = state[i].y;
		block[i * 4 + 2] = state[i].z;
		block[i * 4 + 3] = state[i].w;
	}
	[unroll]
    for (int i = 0; i < CHACHA20_ROUNDS; i += 2)
    {
		Chacha20QuarterRound(block, 0, 4, 8, 12);
		Chacha20QuarterRound(block, 1, 5, 9, 13);
		Chacha20QuarterRound(block, 2, 6, 10, 14);
		Chacha20QuarterRound(block, 3, 7, 11, 15);
		Chacha20QuarterRound(block, 0, 5, 10, 15);
		Chacha20QuarterRound(block, 1, 6, 11, 12);
		Chacha20QuarterRound(block, 2, 7, 8, 13);
		Chacha20QuarterRound(block, 3, 4, 9, 14);
    }
	[unroll]
	for(int i = 0; i < 4; ++i)
	{
		block[i * 4 + 0] += state[i].x;
		block[i * 4 + 1] += state[i].y;
		block[i * 4 + 2] += state[i].z;
		block[i * 4 + 3] += state[i].w;
	}
	//
	data[0] ^= block[0];
	data[1] ^= block[1];
}

void Chacha20XOR(inout uint data[3], uint key[4])
{
	uint4 state[4];
	state[0] = uint4(0x61707865, 0x3320646e, 0x79622d32, 0x6b206574);
	state[1] = uint4(key[0], key[1], key[2], key[3]);
	state[2] = state[1];
	state[3] = uint4(1, 0, 0, 0);

	uint block[16];
	//block
	[unroll]
	for(int i = 0; i < 4; ++i)
	{
		block[i * 4 + 0] = state[i].x;
		block[i * 4 + 1] = state[i].y;
		block[i * 4 + 2] = state[i].z;
		block[i * 4 + 3] = state[i].w;
	}
	[unroll]
    for (int i = 0; i < CHACHA20_ROUNDS; i += 2)
    {
		Chacha20QuarterRound(block, 0, 4, 8, 12);
		Chacha20QuarterRound(block, 1, 5, 9, 13);
		Chacha20QuarterRound(block, 2, 6, 10, 14);
		Chacha20QuarterRound(block, 3, 7, 11, 15);
		Chacha20QuarterRound(block, 0, 5, 10, 15);
		Chacha20QuarterRound(block, 1, 6, 11, 12);
		Chacha20QuarterRound(block, 2, 7, 8, 13);
		Chacha20QuarterRound(block, 3, 4, 9, 14);
    }
	[unroll]
	for(int i = 0; i < 4; ++i)
	{
		block[i * 4 + 0] += state[i].x;
		block[i * 4 + 1] += state[i].y;
		block[i * 4 + 2] += state[i].z;
		block[i * 4 + 3] += state[i].w;
	}
	//
	data[0] ^= block[0];
	data[1] ^= block[1];
	data[2] ^= block[2];
}

half4 DecryptTexture(half2 uv, int m)
{
	half x = frac(uv.x);
	half y = frac(uv.y);

	int idx = (mw[m + WOFFSET] * floor(y * mh[m + HOFFSET])) + floor(x * mw[m + WOFFSET]);
	
	//key make
	uint key[4];
	key[0] = k[0];
	key[1] = k[1];
	key[2] = k[2];
	key[3] = k[3];
	//key make end
	//4idx
	
	half3 pixels[4];
	
	int pos[4] = { 0, -1, -2, -3 };
	int offset = pos[idx % 4];
	pixels[0] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 0 + offset, m, WOFFSET, HOFFSET), m);
	pixels[1] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 1 + offset, m, WOFFSET, HOFFSET), m);
	pixels[2] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 2 + offset, m, WOFFSET, HOFFSET), m);
	pixels[3] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 3 + offset, m, WOFFSET, HOFFSET), m);

	uint data[3] = { 0, 0, 0 };
	data[0] = ((uint)round(pixels[0].r * 255.0f) | ((uint)round(pixels[0].g * 255.0f) << 8) | ((uint)round(pixels[0].b * 255.0f) << 16) | ((uint)round(pixels[1].r * 255.0f) << 24));
	data[1] = ((uint)round(pixels[1].g * 255.0f) | ((uint)round(pixels[1].b * 255.0f) << 8) | ((uint)round(pixels[2].r * 255.0f) << 16) | ((uint)round(pixels[2].g * 255.0f) << 24));
	data[2] = ((uint)round(pixels[2].b * 255.0f) | ((uint)round(pixels[3].r * 255.0f) << 8) | ((uint)round(pixels[3].g * 255.0f) << 16) | ((uint)round(pixels[3].b * 255.0f) << 24));

	XXTEADecrypt(data, key);

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

	int idx = (mw[m + WOFFSET] * floor(y * mh[m + HOFFSET])) + floor(x * mw[m + WOFFSET]);
	
	//key make
	uint key[4];
	key[0] = k[0];
	key[1] = k[1];
	key[2] = k[2];
	key[3] = k[3];
	////key make end
	
	half4 pixels[2];
	
	int offset = (idx & 1) == 0 ? 0 : -1;
	pixels[0] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 0 + offset, m, WOFFSET, HOFFSET), m);
	pixels[1] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 1 + offset, m, WOFFSET, HOFFSET), m);

	uint data[2] = { 0, 0 };
	data[0] = ((uint)round(pixels[0].r * 255.0f) | ((uint)round(pixels[0].g * 255.0f) << 8) | ((uint)round(pixels[0].b * 255.0f) << 16) | ((uint)round(pixels[0].a * 255.0f) << 24));
	data[1] = ((uint)round(pixels[1].r * 255.0f) | ((uint)round(pixels[1].g * 255.0f) << 8) | ((uint)round(pixels[1].b * 255.0f) << 16) | ((uint)round(pixels[1].a * 255.0f) << 24));

	XXTEADecrypt(data, key);

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
	
	uint w = mw[m + WOFFSET];
	uint h = mh[m + HOFFSET];
	
	int idx = (w * floor(y * h)) + floor(x * w);
	
	//key make
	uint key[4];
	key[0] = k[0];
	key[1] = k[1];
	key[2] = k[2];
	key[3] = k[3];
	//key make end
	
	int offset = (idx & 1) == 0 ? 0 : -1;
	
	half4 pixels[2];
	pixels[0] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 0 + offset, m, WOFFSET, HOFFSET), m);
	pixels[1] = _EncryptTex1.SampleLevel(sampler_EncryptTex0, GetUV(idx + 1 + offset, m, WOFFSET, HOFFSET), m);
	
	uint data[2];
	data[0] = ((uint)round(pixels[0].r * 255.0f) | ((uint)round(pixels[0].g * 255.0f) << 8) | ((uint)round(pixels[0].b * 255.0f) << 16) | ((uint)round(pixels[0].a * 255.0f) << 24));
	data[1] = ((uint)round(pixels[1].r * 255.0f) | ((uint)round(pixels[1].g * 255.0f) << 8) | ((uint)round(pixels[1].b * 255.0f) << 16) | ((uint)round(pixels[1].a * 255.0f) << 24));
	
	XXTEADecrypt(data, key);
	
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
#endif
