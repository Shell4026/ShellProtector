using System.Text;
using System.Security.Cryptography;
using System;
using UnityEngine;
public class KeyGenerator
{
    //key1 is fixed key
    //key2 is user key
    public static byte[] MakeKeyBytes(string fixedKey, string userKey, int userKeylength = 4)
    {
        byte[] key = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        byte[] fixedKeyBytes = Encoding.ASCII.GetBytes(fixedKey);
        byte[] userKeyBytes = Encoding.ASCII.GetBytes(userKey);
        byte[] hash = GetKeyHash(userKeyBytes);

        for (int i = 0; i < fixedKeyBytes.Length; ++i)
            key[i] = fixedKeyBytes[i];

        if (userKeylength > 0)
        {
            for (int i = (16 - userKeylength), j = 0; i < key.Length; ++i, ++j)
            {
                if (j < userKeyBytes.Length)
                    key[i] = userKeyBytes[j] ^= hash[j];
                else
                    key[i] = hash[j];
            }
                
        }
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

        System.Random random = new System.Random();
        for (int i = 0; i < length; i++)
        {
            int index = random.Next(chars.Length);
            builder.Append(chars[index]);
        }

        return builder.ToString();
    }
}
