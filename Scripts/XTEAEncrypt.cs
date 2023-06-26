using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
    public static class XTEAEncrypt
    {
        const uint delta3 = 0x11; //magic number but binary start at 1
        const uint delta4 = 0xB9; //magic number but binary start at 1
        const uint delta8 = 0x9E3779B9;
        public static byte[] Encrypt(byte[] data, byte[] key, int rounds = 32)
        {
            if(key.Length != 16)
            {
                Debug.LogWarning("The key requires 16 byte!");
                return null;
            }
            switch(data.Length)
            {
                case 8:
                    return Encrypt8(data, key, rounds);
                case 4:
                    return Encrypt4(data, key, rounds);
                case 3:
                    return Encrypt3(data, key, rounds);
                default:
                    return null;
            }
        }
        public static byte[] Decrypt(byte[] data, byte[] key, int rounds = 32)
        {
            if (key.Length != 16)
            {
                Debug.LogWarning("The key requires 16 byte!");
                return null;
            }
            switch (data.Length)
            {
                case 8:
                    return Decrypt8(data, key, rounds);
                case 4:
                    return Decrypt4(data, key, rounds);
                case 3:
                    return Decrypt3(data, key, rounds);
                default:
                    return null;
            }
        }
        public static byte[] Encrypt8(byte[] data, byte[] key, int rounds = 32)
        {
            if (data.Length != 8)
            {
                Debug.LogWarning("The data requires 8 bytes!");
                return null;
            }
            if (key.Length != 16)
            {
                Debug.LogWarning("The key requires 16 byte!");
                return null;
            }

            //data 8byte, key 16byte
            uint v0 = (uint)(data[0] | data[1] << 8 | data[2] << 16 | data[3] << 24); //4byte
            uint v1 = (uint)(data[4] | data[5] << 8 | data[6] << 16 | data[7] << 24); //4byte
            uint sum = 0; //4byte

            uint[] k = new uint[4];

            k[0] = (uint)(key[0] | key[1] << 8 | key[2] << 16 | key[3] << 24); //4byte
            k[1] = (uint)(key[4] | key[5] << 8 | key[6] << 16 | key[7] << 24); //4byte
            k[2] = (uint)(key[8] | key[9] << 8 | key[10] << 16 | key[11] << 24); //4byte
            k[3] = (uint)(key[12] | key[13] << 8 | key[14] << 16 | key[15] << 24); //4byte
            for (int i = 0; i < rounds; ++i)
            {
                v0 += (v1 << 4 ^ v1 >> 5) + v1 ^ sum + k[sum & 0b11];
                sum += delta8;
                v1 += (v0 << 4 ^ v0 >> 5) + v0 ^ sum + k[sum >> 11 & 0b11];
            }
            return new byte[8]
            {
            (byte)(v0), (byte)(v0 >> 8), (byte)(v0 >> 16), (byte)(v0 >> 24),
            (byte)(v1), (byte)(v1 >> 8), (byte)(v1 >> 16), (byte)(v1 >> 24)
            };
        }
        public static byte[] Decrypt8(byte[] data, byte[] key, int rounds = 32)
        {
            if (data.Length != 8)
            {
                Debug.LogWarning("The data requires 8 bytes!");
                return null;
            }
            if (key.Length != 16)
            {
                Debug.LogWarning("The key requires 16 byte!");
                return null;
            }
            uint v0 = (uint)(data[0] | data[1] << 8 | data[2] << 16 | data[3] << 24); //4byte
            uint v1 = (uint)(data[4] | data[5] << 8 | data[6] << 16 | data[7] << 24); //4byte
            uint sum = (uint)(delta8 * rounds); //4byte

            uint[] k = new uint[4];

            k[0] = (uint)(key[0] | key[1] << 8 | key[2] << 16 | key[3] << 24); //4byte
            k[1] = (uint)(key[4] | key[5] << 8 | key[6] << 16 | key[7] << 24); //4byte
            k[2] = (uint)(key[8] | key[9] << 8 | key[10] << 16 | key[11] << 24); //4byte
            k[3] = (uint)(key[12] | key[13] << 8 | key[14] << 16 | key[15] << 24); //4byte
            for (int i = 0; i < rounds; ++i)
            {
                v1 -= (v0 << 4 ^ v0 >> 5) + v0 ^ sum + k[sum >> 11 & 0b11];
                sum -= delta8;
                v0 -= (v1 << 4 ^ v1 >> 5) + v1 ^ sum + k[sum & 0b11];
            }
            return new byte[8]
            {
            (byte)(v0), (byte)(v0 >> 8), (byte)(v0 >> 16), (byte)(v0 >> 24),
            (byte)(v1), (byte)(v1 >> 8), (byte)(v1 >> 16), (byte)(v1 >> 24)
            };
        }
        public static byte[] Encrypt4(byte[] data, byte[] key, int rounds = 32)
        {
            if (data.Length != 4)
            {
                Debug.LogWarning("The data requires 4 bytes!");
                return null;
            }
            if (key.Length != 16)
            {
                Debug.LogWarning("The key requires 16 byte!");
                return null;
            }

            //data 4byte, key 16byte
            uint v0 = (uint)(data[0] | data[1] << 8); //2byte
            uint v1 = (uint)(data[2] | data[3] << 8); //2byte
            uint sum = 0; //2byte

            uint[] k = new uint[8];

            k[0] = (uint)(key[0] | (key[1] << 8)); //2byte
            k[1] = (uint)(key[2] | (key[3] << 8)); //2byte
            k[2] = (uint)(key[4] | (key[5] << 8)); //2byte
            k[3] = (uint)(key[6] | (key[7] << 8)); //2byte
            k[4] = (uint)(key[8] | (key[9] << 8)); //2byte
            k[5] = (uint)(key[10] | (key[11] << 8)); //2byte
            k[6] = (uint)(key[12] | (key[13] << 8)); //2byte
            k[7] = (uint)(key[14] | (key[15] << 8)); //2byte
            for (int i = 0; i < rounds; ++i)
            {
                v0 += (v1 << 4 ^ v1 >> 5) + v1 ^ sum + k[sum & 0b111];
                v0 &= 0x0000FFFF;
                sum += delta4;
                v1 += (v0 << 4 ^ v0 >> 5) + v0 ^ sum + k[sum >> 3 & 0b111];
                v1 &= 0x0000FFFF;
            }
            return new byte[4] { (byte)(v0), (byte)(v0 >> 8), (byte)(v1), (byte)(v1 >> 8) };
        }
        public static byte[] Decrypt4(byte[] data, byte[] key, int rounds = 32)
        {
            if (data.Length != 4)
            {
                Debug.LogWarning("The data requires 4 bytes!");
                return null;
            }
            if (key.Length != 16)
            {
                Debug.LogWarning("The key requires 16 byte!");
                return null;
            }
            uint v0 = (uint)(data[0] | data[1] << 8); //2byte
            uint v1 = (uint)(data[2] | data[3] << 8); //2byte
            uint sum = (uint)(delta4 * rounds);

            uint[] k = new uint[8];

            k[0] = (uint)(key[0] | (key[1] << 8)); //2byte
            k[1] = (uint)(key[2] | (key[3] << 8)); //2byte
            k[2] = (uint)(key[4] | (key[5] << 8)); //2byte
            k[3] = (uint)(key[6] | (key[7] << 8)); //2byte
            k[4] = (uint)(key[8] | (key[9] << 8)); //2byte
            k[5] = (uint)(key[10] | (key[11] << 8)); //2byte
            k[6] = (uint)(key[12] | (key[13] << 8)); //2byte
            k[7] = (uint)(key[14] | (key[15] << 8)); //2byte
            for (int i = 0; i < rounds; ++i)
            {
                v1 -= (v0 << 4 ^ v0 >> 5) + v0 ^ sum + k[sum >> 3 & 0b111];
                v1 &= 0x0000FFFF;
                sum -= delta4;
                v0 -= (v1 << 4 ^ v1 >> 5) + v1 ^ sum + k[sum & 0b111];
                v0 &= 0x0000FFFF;
            }
            return new byte[4] { (byte)(v0), (byte)(v0 >> 8), (byte)(v1), (byte)(v1 >> 8) };
        }
        public static byte[] Encrypt3(byte[] data, byte[] key, int rounds = 32)
        {
            if (data.Length != 3)
            {
                Debug.LogWarning("The data requires 3 bytes!");
                return null;
            }
            if (key.Length != 16)
            {
                Debug.LogWarning("The key requires 16 byte!");
                return null;
            }

            //data 3byte, key 16byte
            uint v0 = (uint)((data[0] | data[1] << 8) & 0xFFF); //12bit
            uint v1 = (uint)((data[1] >> 4 | data[2] << 4) & 0xFFF); //12bit
            uint sum = 0; //12bit

            uint[] k = new uint[11];

            k[0] = (uint)((key[0]      | (key[1] << 8)) & 0xFFF); //12bit
            k[1] = (uint)((key[1] >> 4 | (key[2] << 4)) & 0xFFF); //12bit

            k[2] = (uint)((key[3]      | (key[4] << 8)) & 0xFFF); //12bit
            k[3] = (uint)((key[4] >> 4 | (key[5] << 4)) & 0xFFF); //12bit

            k[4] = (uint)((key[6]      | (key[7] << 8)) & 0xFFF); //12bit
            k[5] = (uint)((key[7] >> 4 | (key[8] << 4)) & 0xFFF); //12bit

            k[6] = (uint)((key[9]       | (key[10] << 8)) & 0xFFF); //12bit
            k[7] = (uint)((key[10] >> 4 | (key[11] << 4)) & 0xFFF); //12bit

            k[8] = (uint)((key[12]      | (key[13] << 8)) & 0xFFF); //12bit
            k[9] = (uint)((key[13] >> 4 | (key[14] << 4)) & 0xFFF); //12bit

            k[10] = (uint)(key[15]); //8bit

            for (int i = 0; i < rounds; ++i)
            {
                v0 += (v1 << 4 ^ v1 >> 5) + v1 ^ sum + k[sum & 10];
                v0 &= 0x00000FFF;
                sum += delta3;
                v1 += (v0 << 4 ^ v0 >> 5) + v0 ^ sum + k[sum >> 2 & 10];
                v1 &= 0x00000FFF;
            }
            
            
            return new byte[3] { (byte)(v0 & 0xFF), (byte)((v0 >> 8 | v1 << 4) & 0xFF), (byte)((v1 >> 4) & 0xFF) };
        }
        public static byte[] Decrypt3(byte[] data, byte[] key, int rounds = 32)
        {
            if (data.Length != 3)
            {
                Debug.LogWarning("The data requires 3 bytes!");
                return null;
            }
            if (key.Length != 16)
            {
                Debug.LogWarning("The key requires 16 byte!");
                return null;
            }

            //data 3byte, key 16byte
            uint v0 = (uint)((data[0] | data[1] << 8) & 0xFFF); //12bit
            uint v1 = (uint)((data[1] >> 4 | data[2] << 4) & 0xFFF); //12bit
            uint sum = (uint)(delta3 * rounds); //12bit

            uint[] k = new uint[11];

            k[0] = (uint)((key[0] | (key[1] << 8)) & 0xFFF); //12bit
            k[1] = (uint)((key[1] >> 4 | (key[2] << 4)) & 0xFFF); //12bit

            k[2] = (uint)((key[3] | (key[4] << 8)) & 0xFFF); //12bit
            k[3] = (uint)((key[4] >> 4 | (key[5] << 4)) & 0xFFF); //12bit

            k[4] = (uint)((key[6] | (key[7] << 8)) & 0xFFF); //12bit
            k[5] = (uint)((key[7] >> 4 | (key[8] << 4)) & 0xFFF); //12bit

            k[6] = (uint)((key[9] | (key[10] << 8)) & 0xFFF); //12bit
            k[7] = (uint)((key[10] >> 4 | (key[11] << 4)) & 0xFFF); //12bit

            k[8] = (uint)((key[12] | (key[13] << 8)) & 0xFFF); //12bit
            k[9] = (uint)((key[13] >> 4 | (key[14] << 4)) & 0xFFF); //12bit

            k[10] = (uint)(key[15]); //8bit

            for (int i = 0; i < rounds; ++i)
            {
                v1 -= (v0 << 4 ^ v0 >> 5) + v0 ^ sum + k[sum >> 2 & 0b1010];
                v1 &= 0x00000FFF;
                sum -= delta3;
                v0 -= (v1 << 4 ^ v1 >> 5) + v1 ^ sum + k[sum & 0b1010];
                v0 &= 0x00000FFF;
            }
            return new byte[3] { (byte)(v0 & 0xFF), (byte)((v0 >> 8 | v1 << 4) & 0xFF), (byte)((v1 >> 4) & 0xFF) };
        }
    }
}