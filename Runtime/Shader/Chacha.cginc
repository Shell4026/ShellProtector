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

void ChaCha8KeyStream16(const uint key[4], out uint stream[16])
{
    uint x0  = _SHELL_PROTECTOR_CHACHA_C0;
    uint x1  = _SHELL_PROTECTOR_CHACHA_C1;
    uint x2  = _SHELL_PROTECTOR_CHACHA_C2;
    uint x3  = _SHELL_PROTECTOR_CHACHA_C3;

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

    stream[0]  = x0  + _SHELL_PROTECTOR_CHACHA_C0;
    stream[1]  = x1  + _SHELL_PROTECTOR_CHACHA_C1;
    stream[2]  = x2  + _SHELL_PROTECTOR_CHACHA_C2;
    stream[3]  = x3  + _SHELL_PROTECTOR_CHACHA_C3;
    stream[4]  = x4  + key[0];
    stream[5]  = x5  + key[1];
    stream[6]  = x6  + key[2];
    stream[7]  = x7  + key[3];
    stream[8]  = x8  + key[0];
    stream[9]  = x9  + key[1];
    stream[10] = x10 + key[2];
    stream[11] = x11 + key[3];
    stream[12] = x12 + 1u;
    stream[13] = x13 + _Nonce0;
    stream[14] = x14 + _Nonce1;
    stream[15] = x15 + _Nonce2;
}

uint3 ChaCha8KeyStream3(const uint key[4])
{
    uint stream[16];
    ChaCha8KeyStream16(key, stream);
    return uint3(stream[0], stream[1], stream[2]);
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

// Whole 64-byte block: one keystream word per pixel of a 4x4 RGB/RGBA block.
void Decrypt(inout uint data[16], const uint key[4])
{
    uint stream[16];
    ChaCha8KeyStream16(key, stream);

    [unroll]
    for (int i = 0; i < 16; ++i)
        data[i] ^= stream[i];
}