#pragma once
static const uint CHACHA20_ROUNDS = 8;
uint _Nonce0;
uint _Nonce1;
uint _Nonce2;

uint Rotl32(uint x, int n)
{
    return x << n | (x >> (32 - n));
}

void ChaChaQuarterRound(inout uint a, inout uint b, inout uint c, inout uint d)
{
    a += b; d = Rotl32(d ^ a, 16u);
    c += d; b = Rotl32(b ^ c, 12u);
    a += b; d = Rotl32(d ^ a, 8u);
    c += d; b = Rotl32(b ^ c, 7u);
}

void Chacha20QuarterRound(inout uint state[16], int a, int b, int c, int d)
{
	state[a] += state[b]; state[d] = Rotl32(state[d] ^ state[a], 16);
	state[c] += state[d]; state[b] = Rotl32(state[b] ^ state[c], 12);
	state[a] += state[b]; state[d] = Rotl32(state[d] ^ state[a], 8);
	state[c] += state[d]; state[b] = Rotl32(state[b] ^ state[c], 7);
}

uint3 ChaCha8KeyStream3(const uint key[4])
{
    uint x0  = 0x61707865u;
    uint x1  = 0x3320646eu;
    uint x2  = 0x79622d32u;
    uint x3  = 0x6b206574u;

    uint x4  = key[0];
    uint x5  = key[1];
    uint x6  = key[2];
    uint x7  = key[3];

    uint x8  = key[0];
    uint x9  = key[1];
    uint x10 = key[2];
    uint x11 = key[3];

    uint x12 = 1u;
    uint x13 = _Nonce0;
    uint x14 = _Nonce1;
    uint x15 = _Nonce2;

    [unroll]
    for (uint round = 0u; round < CHACHA20_ROUNDS; round += 2u)
    {
        // Column round
        ChaChaQuarterRound(x0, x4, x8,  x12);
        ChaChaQuarterRound(x1, x5, x9,  x13);
        ChaChaQuarterRound(x2, x6, x10, x14);
        ChaChaQuarterRound(x3, x7, x11, x15);

        // Diagonal round
        ChaChaQuarterRound(x0, x5, x10, x15);
        ChaChaQuarterRound(x1, x6, x11, x12);
        ChaChaQuarterRound(x2, x7, x8,  x13);
        ChaChaQuarterRound(x3, x4, x9,  x14);
    }

    return uint3(
        x0 + 0x61707865u,
        x1 + 0x3320646eu,
        x2 + 0x79622d32u
    );
}

void Decrypt(inout uint data[2], const uint key[4])
{
	uint3 stream = ChaCha8KeyStream3(key);
    data[0] ^= stream.x;
    data[1] ^= stream.y;
}

void Decrypt(inout uint data[3], const uint key[4])
{
    uint3 stream = ChaCha8KeyStream3(key);

    data[0] ^= stream.x;
    data[1] ^= stream.y;
    data[2] ^= stream.z;
}