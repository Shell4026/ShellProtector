using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
public class KeyGenerator
{
    //key1 is fixed key
    //key2 is user key
    public static byte[] MakeKeyBytes(string _key1, string _key2, int key2_length = 4)
    {
        SHA256 sha256 = SHA256.Create();

        byte[] key = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        byte[] key_bytes = Encoding.ASCII.GetBytes(_key1);
        byte[] key_bytes2 = Encoding.ASCII.GetBytes(_key2);
        byte[] hash = sha256.ComputeHash(key_bytes2);

        for (int i = 0; i < key_bytes.Length; ++i)
            key[i] = key_bytes[i];

        if (key2_length > 0)
        {
            if (key2_length == 16)
            {
                for (int i = 0; i < key_bytes2.Length; ++i)
                    key[i] = key_bytes2[i];
            }
            else
            {
                for (int i = 0; i < key_bytes2.Length; ++i)
                    key[i + (16 - key2_length)] = key_bytes2[i];
            }
            for (int i = 0; i < key2_length; ++i)
                key[i + (16 - key2_length)] ^= hash[i];
        }
        return key;
    }
}
