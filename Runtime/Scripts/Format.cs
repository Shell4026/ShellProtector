using Shell.Protector;
using UnityEngine;

public struct EncryptResult {
    public Texture2D Texture1;
    public Texture2D Texture2;
}

public interface ITextureFormat {
    bool CanHandle(TextureFormat format);
    EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm);
    void SetFormatKeywords(Material material);
    (int, int) CalculateOffsets(Texture2D texture);
}

public abstract class BaseTextureFormat : ITextureFormat {
    protected uint[] ConvertKeyToUInt(byte[] key) {
        uint[] key_uint = new uint[4];
        key_uint[0] = (uint)(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
        key_uint[1] = (uint)(key[4] | (key[5] << 8) | (key[6] << 16) | (key[7] << 24));
        key_uint[2] = (uint)(key[8] | (key[9] << 8) | (key[10] << 16) | (key[11] << 24));
        key_uint[3] = 0;
        return key_uint;
    }

    protected int GetCanMipmapLevel(int w, int h) {
        if (w < 1 || h <= 1) return 0;
        int w_level = (int)Mathf.Log(w, 2);
        int h_level = (int)Mathf.Log(h, 2);
        return Mathf.Max(w_level, h_level);
    }

    public abstract bool CanHandle(TextureFormat format);
    public abstract EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm);
    public abstract void SetFormatKeywords(Material material);
    public abstract (int, int) CalculateOffsets(Texture2D texture);
}