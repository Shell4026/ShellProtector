using Shell.Protector;
using UnityEngine;

public class Test
{
    public static void XXTEATest(string fixedKey, string userKey, int userKeySize)
    {
        byte[] data_byte = new byte[12] { 255, 250, 245, 240, 235, 230, 225, 220, 215, 210, 205, 200 };
        byte[] key_byte = KeyGenerator.MakeKeyBytes(fixedKey, userKey, userKeySize);

        uint[] data = new uint[3];
        data[0] = (uint)(data_byte[0] | (data_byte[1] << 8) | (data_byte[2] << 16) | (data_byte[3] << 24));
        data[1] = (uint)(data_byte[4] | (data_byte[5] << 8) | (data_byte[6] << 16) | (data_byte[7] << 24));
        data[2] = (uint)(data_byte[8] | (data_byte[9] << 8) | (data_byte[10] << 16) | (data_byte[11] << 24));

        uint[] key = new uint[4];
        key[0] = (uint)(key_byte[0] | (key_byte[1] << 8) | (key_byte[2] << 16) | (key_byte[3] << 24));
        key[1] = (uint)(key_byte[4] | (key_byte[5] << 8) | (key_byte[6] << 16) | (key_byte[7] << 24));
        key[2] = (uint)(key_byte[8] | (key_byte[9] << 8) | (key_byte[10] << 16) | (key_byte[11] << 24));
        key[3] = (uint)(key_byte[12] | (key_byte[13] << 8) | (key_byte[14] << 16) | (key_byte[15] << 24));

        Debug.Log("Key bytes: " + string.Join(", ", key_byte));
        Debug.Log(string.Format("key1:{0}, key2:{1}, key3:{2}", key[0], key[1], key[2]));
        Debug.Log("Data: " + string.Join(", ", data));

        XXTEA xxtea = new XXTEA();
        uint[] result = xxtea.Encrypt(data, key);
        Debug.Log("Encrypted data: " + string.Join(", ", result));

        result = xxtea.Decrypt(result, key);
        Debug.Log("Decrypted data: " + string.Join(", ", result));
    }
    public static void ChachaTest(string fixedKey, string userKey, int userKeySize)
    {
        byte[] data_byte = new byte[8] { 255, 255, 245, 240, 235, 230, 225, 220 };
        byte[] key_byte = KeyGenerator.MakeKeyBytes(fixedKey, userKey, userKeySize);

        uint[] data = new uint[2];
        data[0] = (uint)(data_byte[0] | (data_byte[1] << 8) | (data_byte[2] << 16) | (data_byte[3] << 24));
        data[1] = (uint)(data_byte[4] | (data_byte[5] << 8) | (data_byte[6] << 16) | (data_byte[7] << 24));

        uint[] key = new uint[4];
        key[0] = (uint)(key_byte[0] | (key_byte[1] << 8) | (key_byte[2] << 16) | (key_byte[3] << 24));
        key[1] = (uint)(key_byte[4] | (key_byte[5] << 8) | (key_byte[6] << 16) | (key_byte[7] << 24));
        key[2] = (uint)(key_byte[8] | (key_byte[9] << 8) | (key_byte[10] << 16) | (key_byte[11] << 24));
        key[3] = (uint)(key_byte[12] | (key_byte[13] << 8) | (key_byte[14] << 16) | (key_byte[15] << 24));

        Debug.Log("Key bytes: " + string.Join(", ", key_byte));
        Debug.Log(string.Format("key1:{0}, key2:{1}, key3:{2}", key[0], key[1], key[2]));
        Debug.Log("Data: " + string.Join(", ", data));

        Chacha20 chacha = new Chacha20();
        uint[] result = chacha.Encrypt(data, key);
        Debug.Log("Encrypted data: " + string.Join(", ", result));
        result = chacha.Encrypt(result, key);
        Debug.Log("Decrypted data: " + string.Join(", ", result));
    }
}
