using System;
using System.Runtime.CompilerServices;

namespace Shell.Protector
{
    public class Chacha20 : IEncryptor
    {
        const int Rounds = 8;

        public string Keyword => ShaderProperties.ChachaKeyword;
        public byte[] Nonce { get; } = new byte[12];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Rotl32(uint x, int n)
        {
            return x << n | (x >> (32 - n));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
        {
            a += b; d = Rotl32(d ^ a, 16);
            c += d; b = Rotl32(b ^ c, 12);
            a += b; d = Rotl32(d ^ a, 8);
            c += d; b = Rotl32(b ^ c, 7);
        }

        uint NonceWord(int i)
        {
            return (uint)(Nonce[i * 4] | (Nonce[i * 4 + 1] << 8) | (Nonce[i * 4 + 2] << 16) | (Nonce[i * 4 + 3] << 24));
        }

        // XORs the keystream into data in place without allocating; the block counter starts at 1.
        // The 16-byte key fills both key rows of the state, the same as Chacha.cginc.
        // Only reads the key and Nonce, so it is safe to call from several threads.
        public void XorKeyStream(Span<uint> data, uint[] key)
        {
            uint k0 = key[0], k1 = key[1], k2 = key[2], k3 = key[3];
            uint n0 = NonceWord(0), n1 = NonceWord(1), n2 = NonceWord(2);
            Span<uint> stream = stackalloc uint[16];

            uint counter = 1;
            for (int offset = 0; offset < data.Length; offset += 16, ++counter)
            {
                uint x0 = 0x61707865, x1 = 0x3320646e, x2 = 0x79622d32, x3 = 0x6b206574;
                uint x4 = k0, x5 = k1, x6 = k2, x7 = k3;
                uint x8 = k0, x9 = k1, x10 = k2, x11 = k3;
                uint x12 = counter, x13 = n0, x14 = n1, x15 = n2;

                for (int i = 0; i < Rounds; i += 2)
                {
                    QuarterRound(ref x0, ref x4, ref x8, ref x12);
                    QuarterRound(ref x1, ref x5, ref x9, ref x13);
                    QuarterRound(ref x2, ref x6, ref x10, ref x14);
                    QuarterRound(ref x3, ref x7, ref x11, ref x15);
                    QuarterRound(ref x0, ref x5, ref x10, ref x15);
                    QuarterRound(ref x1, ref x6, ref x11, ref x12);
                    QuarterRound(ref x2, ref x7, ref x8, ref x13);
                    QuarterRound(ref x3, ref x4, ref x9, ref x14);
                }

                stream[0] = x0 + 0x61707865; stream[1] = x1 + 0x3320646e; stream[2] = x2 + 0x79622d32; stream[3] = x3 + 0x6b206574;
                stream[4] = x4 + k0; stream[5] = x5 + k1; stream[6] = x6 + k2; stream[7] = x7 + k3;
                stream[8] = x8 + k0; stream[9] = x9 + k1; stream[10] = x10 + k2; stream[11] = x11 + k3;
                stream[12] = x12 + counter; stream[13] = x13 + n0; stream[14] = x14 + n1; stream[15] = x15 + n2;

                int count = Math.Min(16, data.Length - offset);
                for (int i = 0; i < count; ++i)
                    data[offset + i] ^= stream[i];
            }
        }

        public uint[] Encrypt(uint[] data, uint[] key)
        {
            uint[] result = (uint[])data.Clone();
            XorKeyStream(result, key);
            return result;
        }
        public uint[] Decrypt(uint[] data, uint[] key)
        {
            return Encrypt(data, key);
        }

        public uint[] GetNonceUint3()
        {
            return new[] { NonceWord(0), NonceWord(1), NonceWord(2) };
        }
    }
}
