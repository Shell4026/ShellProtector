public interface IEncryptor
{
    public uint[] Encrypt(uint[] data, uint[] key);
    public uint[] Decrypt(uint[] data, uint[] key);
}