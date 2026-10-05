using System.Text;
using System.Security.Cryptography;
using System;
using UnityEngine;

namespace Shell.Protector
{
public class KeyGenerator
{
    // The fixed key fills the key from the front and the user key (see UserKey) fills its last bytes.
    public static byte[] MakeKeyBytes(string fixedKey, byte[] userKey)
    {
        byte[] key = new byte[16];
        byte[] fixedKeyBytes = Encoding.ASCII.GetBytes(fixedKey);

        for (int i = 0; i < fixedKeyBytes.Length && i < key.Length; ++i)
            key[i] = fixedKeyBytes[i];

        Array.Copy(userKey, 0, key, key.Length - userKey.Length, userKey.Length);
        return key;
    }

    public static byte[] GetKeyHash(byte[] key, string salt = null)
    {
        SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(key);
        if (salt == null)
        {
            return hash;
        }

        byte[] saltBytes = Encoding.ASCII.GetBytes(salt);
        for(int i = 0; i < hash.Length; ++i)
            hash[i] ^= saltBytes[i % saltBytes.Length];

        return hash;
    }

    public static byte[] GetHash(int data)
    {
        SHA256 sha256 = SHA256.Create();
        byte[] bytes = BitConverter.GetBytes(data);
        byte[] hash = sha256.ComputeHash(bytes);
        return hash;
    }

    public static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_-+=|\\/?.>,<~`\'\" ";
        StringBuilder builder = new StringBuilder();

        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            byte[] buffer = new byte[4];
            for (int i = 0; i < length; i++)
            {
                random.GetBytes(buffer);
                int index = (int)(BitConverter.ToUInt32(buffer, 0) % chars.Length);
                builder.Append(chars[index]);
            }
        }

        return builder.ToString();
    }

    public static uint SimpleHash(byte[] data, uint hashMagic)
    {
        if (data.Length != 16)
            throw new ArgumentException("Input must be exactly 16 bytes.");

        uint hash = 0x811C9DC5u;
        hash *= hashMagic;

        for (int i = 0; i < 16; i++)
        {
            uint k = data[i];

            k *= 0xcc9e2d51u;
            k = (k << 15) | (k >> 17);
            k *= 0x1b873593u;

            hash ^= k;
            hash = (hash << 13) | (hash >> 19);
            hash = hash * 5u + 0xe6546b64u;
        }

        hash ^= 16u;
        hash ^= (hash >> 16);
        hash *= 0x85ebca6bu;
        hash ^= (hash >> 13);
        hash *= 0xc2b2ae35u;
        hash ^= (hash >> 16);

        return hash;
    }
}
}
