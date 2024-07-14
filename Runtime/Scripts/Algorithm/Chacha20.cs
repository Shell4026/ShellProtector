using System;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

public class Chacha20 : IEncryptor
{
    public byte[] noce = new byte[12];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    byte[] U32t8le(uint v)
    {
        byte[] p = new byte[4];
        p[0] = (byte)(v & 0xff);
        p[1] = (byte)((v >> 8) & 0xff);
        p[2] = (byte)((v >> 16) & 0xff);
        p[3] = (byte)((v >> 24) & 0xff);

        return p;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    uint U8t32le(byte[] p)
    {
        uint value = p[3];

        value = (value << 8) | p[2];
        value = (value << 8) | p[1];
        value = (value << 8) | p[0];

        return value;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    uint Rotl32(uint x, int n)
    {
        // http://blog.regehr.org/archives/1063
        return x << n | (x >> (-n & 31));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Chacha20QuarterRound(uint[] x, int a, int b, int c, int d)
    {
        x[a] += x[b]; x[d] = Rotl32(x[d] ^ x[a], 16);
        x[c] += x[d]; x[b] = Rotl32(x[b] ^ x[c], 12);
        x[a] += x[b]; x[d] = Rotl32(x[d] ^ x[a], 8);
        x[c] += x[d]; x[b] = Rotl32(x[b] ^ x[c], 7);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    byte[] Chacha20Serialize(uint[] input)
    {
        byte[] output = new byte[64];
        for (int i = 0; i < 16; i++)
        {
            byte[] tempBytes = U32t8le(input[i]);
            Array.Copy(tempBytes, 0, output, i << 2, tempBytes.Length);
        }
        return output;
    }
    byte[] Chacha20Block(uint[] input, int num_rounds)
    {
        byte[] output = new byte[64];
        uint[] x = new uint[16];
        Array.Copy(input, 0, x, 0, input.Length);
        for (int i = num_rounds; i > 0; i -= 2)
        {
            Chacha20QuarterRound(x, 0, 4, 8, 12);
            Chacha20QuarterRound(x, 1, 5, 9, 13);
            Chacha20QuarterRound(x, 2, 6, 10, 14);
            Chacha20QuarterRound(x, 3, 7, 11, 15);
            Chacha20QuarterRound(x, 0, 5, 10, 15);
            Chacha20QuarterRound(x, 1, 6, 11, 12);
            Chacha20QuarterRound(x, 2, 7, 8, 13);
            Chacha20QuarterRound(x, 3, 4, 9, 14);
        }

        for (int i = 0; i < 16; i++)
        {
            x[i] += input[i] ;
        }

        output = Chacha20Serialize(x);

        return output;
    }
    //key 16byte, nonce 12byte
    void Chacha20Init(uint[] s, byte[] key, uint counter, byte[] nonce)
    {
        // refer: https://dxr.mozilla.org/mozilla-beta/source/security/nss/lib/freebl/chacha20.c
        // convert magic number to string: "expand 32-byte k"
        s[0] = 0x61707865;
        s[1] = 0x3320646e;
        s[2] = 0x79622d32;
        s[3] = 0x6b206574;

        for (int i = 0; i < 4; i++)
        {
            byte[] key_tmp = new byte[4];
            Array.Copy(key, i * 4, key_tmp, 0, key_tmp.Length);
            s[4 + i] = U8t32le(key_tmp);
            s[8 + i] = s[4 + i];
        }

        s[12] = counter;

        for (int i = 0; i < 3; i++)
        {
            byte[] nonce_tmp = new byte[4];
            Array.Copy(nonce, i * 4, nonce_tmp, 0, nonce_tmp.Length);
            s[13 + i] = U8t32le(nonce_tmp);
        }
    }

    //key 16byte nonce 12byte
    public byte[] ChaCha20XOR(byte[] key, uint counter, byte[] nonce, byte[] input) 
    {
        uint[] s = new uint[16];
        byte[] block = new byte[64];
        byte[] output = new byte[input.Length];

        Chacha20Init(s, key, counter, nonce);

        for (int i = 0; i < input.Length; i += 64) {
            block = Chacha20Block(s, 8);
            s[12]++;

            for (int j = i; j < i + 64; j++) {
                if (j >= input.Length) {
                    break;
                }
                output[j] = (byte)(input[j] ^ block[j - i]);
            }
        }
        return output;
    }

    //data 8byte
    public uint[] Encrypt(uint[] data, uint[] key)
    {
        byte[] keyBytes = new byte[16];
        byte[] dataBytes = new byte[data.Length * 4];

        for(int i = 0; i < keyBytes.Length; i += 4)
        {
            keyBytes[i + 0] = (byte)(key[i / 4] >> 0 & 0xFF);
            keyBytes[i + 1] = (byte)(key[i / 4] >> 8 & 0xFF);
            keyBytes[i + 2] = (byte)(key[i / 4] >> 16 & 0xFF);
            keyBytes[i + 3] = (byte)(key[i / 4] >> 24 & 0xFF);
        }
        for (int i = 0; i < dataBytes.Length; i += 4)
        {
            dataBytes[i + 0] = (byte)(data[i / 4] >> 0 & 0xFF);
            dataBytes[i + 1] = (byte)(data[i / 4] >> 8 & 0xFF);
            dataBytes[i + 2] = (byte)(data[i / 4] >> 16 & 0xFF);
            dataBytes[i + 3] = (byte)(data[i / 4] >> 24 & 0xFF);
        }

        byte[] resultBytes = ChaCha20XOR(keyBytes, 1, noce, dataBytes);
        uint[] result = new uint[data.Length];
        for(int i = 0; i < data.Length; ++i)
        {
            result[i] = (uint)((resultBytes[i * 4 + 0]) | (resultBytes[i * 4 + 1] << 8) | (resultBytes[i * 4 + 2] << 16) | (resultBytes[i * 4 + 3] << 24));
        }
        return result;
    }
    public uint[] Decrypt(uint[] data, uint[] key)
    {
        return Encrypt(data, key);
    }

}