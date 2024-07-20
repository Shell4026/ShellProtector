#ifndef XXTEA
#define XXTEA

static const uint Delta = 0x9e3779b9;
static const uint ROUNDS = 6;
static const uint SUM = Delta * ROUNDS;

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
#endif