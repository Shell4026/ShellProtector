using System;
using UnityEngine;

namespace Shell.Protector
{
    public class XXTEA : IEncryptor
    {
        const uint Delta = 0x9E3779B9;

        public uint m_rounds = 0;
        public uint[] Encrypt(uint[] data, uint[] key)
        {
            uint n = (uint)data.Length;

            uint[] result = new uint[n];
            Array.Copy(data, result, n);
            if (n < 2)
            {
                Debug.LogError("Data must be minimum 8 bytes!");
                return null;
            }
            if (key.Length != 4)
            {
                Debug.LogError("Key must be 16 bytes!");
                return null;
            }
            uint y, z, sum;
            uint p, rounds, e;

            if (m_rounds == 0)
                rounds = 6 + 52 / n;
            else
                rounds = m_rounds;
            sum = 0;
            z = result[n - 1];
            do
            {
                sum += Delta;
                e = (sum >> 2) & 3;
                for (p = 0; p < n - 1; p++)
                {
                    y = result[p + 1];
                    z = result[p] += (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)));
                }
                y = result[0];
                z = result[n - 1] += (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)));
            } while (--rounds > 0);
            return result;
        }
        public uint[] Decrypt(uint[] data, uint[] key)
        {
            uint n = (uint)data.Length;

            uint[] result = new uint[n];
            Array.Copy(data, result, n);

            if (n < 2)
            {
                Debug.LogError("Data must be minimum 8 bytes!");
                return null;
            }
            uint y, z, sum;
            uint p, rounds, e;

            if (m_rounds == 0)
                rounds = 6 + 52 / n;
            else
                rounds = m_rounds;
            sum = rounds * Delta;

            y = result[0];
            do
            {
                e = (sum >> 2) & 3;
                for (p = n - 1; p > 0; p--)
                {
                    z = result[p - 1];
                    y = result[p] -= (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)));
                }
                z = result[n - 1];
                y = result[0] -= (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)));
                sum -= Delta;
            } while (--rounds > 0);
            return result;
        }
    }
}