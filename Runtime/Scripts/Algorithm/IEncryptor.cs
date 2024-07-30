namespace Shell.Protector
{
    public interface IEncryptor
    {
#if UNITY_2022
        public uint[] Encrypt(uint[] data, uint[] key);
        public uint[] Decrypt(uint[] data, uint[] key);
#else
        uint[] Encrypt(uint[] data, uint[] key);
        uint[] Decrypt(uint[] data, uint[] key);
#endif
    }
}