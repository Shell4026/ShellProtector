interface IEncryptor
{
    uint[] Encrypt(uint[] data, uint[] key);
    uint[] Decrypt(uint[] data, uint[] key);
}