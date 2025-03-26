#pragma once
static const uint CHACHA20_ROUNDS = 8;
uint _Nonce0;
uint _Nonce1;
uint _Nonce2;

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

void Decrypt(inout uint data[2], const uint key[4])
{
	uint4 state[4];
	state[0] = uint4(0x61707865, 0x3320646e, 0x79622d32, 0x6b206574);
	state[1] = uint4(key[0], key[1], key[2], key[3]);
	state[2] = state[1];
	state[3] = uint4(1, _Nonce0, _Nonce1, _Nonce2);

	uint block[16];
	//block
	[unroll]
	int i = 0;
	for(i = 0; i < 4; ++i)
	{
		block[i * 4 + 0] = state[i].x;
		block[i * 4 + 1] = state[i].y;
		block[i * 4 + 2] = state[i].z;
		block[i * 4 + 3] = state[i].w;
	}
	[unroll]
    for (i = 0; i < CHACHA20_ROUNDS; i += 2)
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
	for(i = 0; i < 4; ++i)
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

void Decrypt(inout uint data[3], const uint key[4])
{
	uint4 state[4];
	state[0] = uint4(0x61707865, 0x3320646e, 0x79622d32, 0x6b206574);
	state[1] = uint4(key[0], key[1], key[2], key[3]);
	state[2] = state[1];
	state[3] = uint4(1, _Nonce0, _Nonce1, _Nonce2);

	uint block[16];
	//block
	int i = 0;
	[unroll]
	for(i = 0; i < 4; ++i)
	{
		block[i * 4 + 0] = state[i].x;
		block[i * 4 + 1] = state[i].y;
		block[i * 4 + 2] = state[i].z;
		block[i * 4 + 3] = state[i].w;
	}
	[unroll]
    for (i = 0; i < CHACHA20_ROUNDS; i += 2)
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
	for(i = 0; i < 4; ++i)
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