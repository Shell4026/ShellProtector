#ifndef Decrypt
#define Decrypt

static uint mw[12] = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };
static uint mh[12] = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };

static uint k[8] = { 0, 0, 0, 0, 0, 0, 0, 0 };

static const uint delta = 0xB9; //magic number but binary start at 1
static const uint rounds = 32;

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

float4 XTEADecrypt(float4 pixel, int n)
{
	uint data[4] = {round(pixel.r * 255.0f), round(pixel.g * 255.0f), round(pixel.b * 255.0f), round(pixel.a * 255.0f)};
	
	uint v0 = (data[0] | (data[1] << 8)); //2byte
    uint v1 = (data[2] | (data[3] << 8)); //2byte
	
	k[6] = (n & 0x0000FFFF);
	k[7] = n >> 16;
	
	uint sum = delta * rounds; //128
	for(int i = 0; i < rounds; ++i)
	{
		v1 -= (v0 << 4 ^ v0 >> 5) + v0 ^ sum + k[sum >> 3 & 7];
		v1 &= 0x0000FFFF;
		sum -= delta;
		v0 -= (v1 << 4 ^ v1 >> 5) + v1 ^ sum + k[sum & 7];
		v0 &= 0x0000FFFF;
	}
	
	float r = (v0 & 0x000000FF)/255.0f;
	float g = (v0 >> 8)/255.0f;
	float b = (v1 & 0x000000FF)/255.0f;
	float a = (v1 >> 8)/255.0f;

	return float4(r, g, b, a);
}

float4 DecryptTexture(float4 pixel, float2 uv, int m)
{
	float x = uv.x;
	float y = uv.y;

	int mip = 11 - m;
	int idx = (mw[mip] * floor(y * mh[mip])) + floor(x * mw[mip]);
	
	float4 decrypt = XTEADecrypt(pixel, idx);

	return float4(GammaCorrection(decrypt.rgb), decrypt.a);
}
#endif
