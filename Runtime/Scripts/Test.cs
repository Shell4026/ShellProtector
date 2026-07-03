using Shell.Protector;
using UnityEngine;

namespace Shell.Protector
{
public class Test
{
    public static void XXTEATest(string fixedKey, string userKey, int userKeySize)
    {
        byte[] dataBytes = new byte[12] { 255, 250, 245, 240, 235, 230, 225, 220, 215, 210, 205, 200 };
        byte[] keyBytes = KeyGenerator.MakeKeyBytes(fixedKey, userKey, userKeySize);

        uint[] data = new uint[3];
        data[0] = (uint)(dataBytes[0] | (dataBytes[1] << 8) | (dataBytes[2] << 16) | (dataBytes[3] << 24));
        data[1] = (uint)(dataBytes[4] | (dataBytes[5] << 8) | (dataBytes[6] << 16) | (dataBytes[7] << 24));
        data[2] = (uint)(dataBytes[8] | (dataBytes[9] << 8) | (dataBytes[10] << 16) | (dataBytes[11] << 24));

        uint[] key = new uint[4];
        key[0] = (uint)(keyBytes[0] | (keyBytes[1] << 8) | (keyBytes[2] << 16) | (keyBytes[3] << 24));
        key[1] = (uint)(keyBytes[4] | (keyBytes[5] << 8) | (keyBytes[6] << 16) | (keyBytes[7] << 24));
        key[2] = (uint)(keyBytes[8] | (keyBytes[9] << 8) | (keyBytes[10] << 16) | (keyBytes[11] << 24));
        key[3] = (uint)(keyBytes[12] | (keyBytes[13] << 8) | (keyBytes[14] << 16) | (keyBytes[15] << 24));

        Debug.Log("Key bytes: " + string.Join(", ", keyBytes));
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
        byte[] dataBytes = new byte[8] { 255, 255, 245, 240, 235, 230, 225, 220 };
        byte[] keyBytes = KeyGenerator.MakeKeyBytes(fixedKey, userKey, userKeySize);

        uint[] data = new uint[2];
        data[0] = (uint)(dataBytes[0] | (dataBytes[1] << 8) | (dataBytes[2] << 16) | (dataBytes[3] << 24));
        data[1] = (uint)(dataBytes[4] | (dataBytes[5] << 8) | (dataBytes[6] << 16) | (dataBytes[7] << 24));

        uint[] key = new uint[4];
        key[0] = (uint)(keyBytes[0] | (keyBytes[1] << 8) | (keyBytes[2] << 16) | (keyBytes[3] << 24));
        key[1] = (uint)(keyBytes[4] | (keyBytes[5] << 8) | (keyBytes[6] << 16) | (keyBytes[7] << 24));
        key[2] = (uint)(keyBytes[8] | (keyBytes[9] << 8) | (keyBytes[10] << 16) | (keyBytes[11] << 24));
        key[3] = (uint)(keyBytes[12] | (keyBytes[13] << 8) | (keyBytes[14] << 16) | (keyBytes[15] << 24));

        Debug.Log("Key bytes: " + string.Join(", ", keyBytes));
        Debug.Log(string.Format("key1:{0}, key2:{1}, key3:{2}", key[0], key[1], key[2]));
        Debug.Log("Data: " + string.Join(", ", data));

        Chacha20 chacha = new Chacha20();
        uint[] result = chacha.Encrypt(data, key);
        Debug.Log("Encrypted data: " + string.Join(", ", result));
        result = chacha.Encrypt(result, key);
        Debug.Log("Decrypted data: " + string.Join(", ", result));
    }
}
}
