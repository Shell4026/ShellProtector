using UnityEngine;
using Shell.Protector;

public abstract class RGBFormat : BaseTextureFormat {
    protected Texture2D CreateResultTexture(Texture2D texture, TextureFormat format) {
        int mip_lv = GetCanMipmapLevel(texture.width, texture.height);
        var result = new Texture2D(texture.width, texture.height, format, mip_lv - 2, true);
        result.filterMode = FilterMode.Point;
        result.anisoLevel = 0;
        return result;
    }
}

public class RGB24Format : RGBFormat {
    public override bool CanHandle(TextureFormat format) {
        return format == TextureFormat.RGB24;
    }

    public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm) {
        var result = new EncryptResult();
        result.Texture1 = CreateResultTexture(texture, TextureFormat.RGB24);

        var key_uint = ConvertKeyToUInt(key);

        for (int m = 0; m < result.Texture1.mipmapCount; ++m) {
            Color32[] pixels = texture.GetPixels32(m);

            for (int i = 0; i < pixels.Length; i += 4) {
                key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                key_uint[3] ^= (uint)i;

                uint[] data = new uint[3];
                data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 1].r << 24));
                data[1] = (uint)(pixels[i + 1].g + (pixels[i + 1].b << 8) + (pixels[i + 2].r << 16) + (pixels[i + 2].g << 24));
                data[2] = (uint)(pixels[i + 2].b + (pixels[i + 3].r << 8) + (pixels[i + 3].g << 16) + (pixels[i + 3].b << 24));

                uint[] data_enc = algorithm.Encrypt(data, key_uint);

                pixels[i + 0].r = (byte)((data_enc[0] & 0x000000FF) >> 0);
                pixels[i + 0].g = (byte)((data_enc[0] & 0x0000FF00) >> 8);
                pixels[i + 0].b = (byte)((data_enc[0] & 0x00FF0000) >> 16);
                pixels[i + 1].r = (byte)((data_enc[0] & 0xFF000000) >> 24);
                pixels[i + 1].g = (byte)((data_enc[1] & 0x000000FF) >> 0);
                pixels[i + 1].b = (byte)((data_enc[1] & 0x0000FF00) >> 8);
                pixels[i + 2].r = (byte)((data_enc[1] & 0x00FF0000) >> 16);
                pixels[i + 2].g = (byte)((data_enc[1] & 0xFF000000) >> 24);
                pixels[i + 2].b = (byte)((data_enc[2] & 0x000000FF) >> 0);
                pixels[i + 3].r = (byte)((data_enc[2] & 0x0000FF00) >> 8);
                pixels[i + 3].g = (byte)((data_enc[2] & 0x00FF0000) >> 16);
                pixels[i + 3].b = (byte)((data_enc[2] & 0xFF000000) >> 24);
            }
            result.Texture1.SetPixels32(pixels, m);
        }

        return result;
    }

    public override void SetFormatKeywords(Material material) {
        material.EnableKeyword("_SHELL_PROTECTOR_FORMAT0");
        material.DisableKeyword("_SHELL_PROTECTOR_FORMAT1");
    }

    public override (int, int) CalculateOffsets(Texture2D texture) {
        int woffset = 13 - (int)Mathf.Log(texture.width, 2) - 1;
        int hoffset = 13 - (int)Mathf.Log(texture.height, 2) - 1;
        return (woffset, hoffset);
    }
}

public class RGBA32Format : RGBFormat {
    public override bool CanHandle(TextureFormat format) {
        return format == TextureFormat.RGBA32;
    }

    public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm) {
        var result = new EncryptResult();
        result.Texture1 = CreateResultTexture(texture, TextureFormat.RGBA32);

        var key_uint = ConvertKeyToUInt(key);

        for (int m = 0; m < result.Texture1.mipmapCount; ++m) {
            Color32[] pixels = texture.GetPixels32(m);

            for (int i = 0; i < pixels.Length; i += 2) {
                key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                key_uint[3] ^= (uint)i;

                uint[] data = new uint[2];
                data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 0].a << 24));
                data[1] = (uint)(pixels[i + 1].r + (pixels[i + 1].g << 8) + (pixels[i + 1].b << 16) + (pixels[i + 1].a << 24));

                uint[] data_enc = algorithm.Encrypt(data, key_uint);

                pixels[i + 0].r = (byte)((data_enc[0] & 0x000000FF) >> 0);
                pixels[i + 0].g = (byte)((data_enc[0] & 0x0000FF00) >> 8);
                pixels[i + 0].b = (byte)((data_enc[0] & 0x00FF0000) >> 16);
                pixels[i + 0].a = (byte)((data_enc[0] & 0xFF000000) >> 24);
                pixels[i + 1].r = (byte)((data_enc[1] & 0x000000FF) >> 0);
                pixels[i + 1].g = (byte)((data_enc[1] & 0x0000FF00) >> 8);
                pixels[i + 1].b = (byte)((data_enc[1] & 0x00FF0000) >> 16);
                pixels[i + 1].a = (byte)((data_enc[1] & 0xFF000000) >> 24);
            }
            result.Texture1.SetPixels32(pixels, m);
        }

        return result;
    }

    public override void SetFormatKeywords(Material material) {
        material.DisableKeyword("_SHELL_PROTECTOR_FORMAT0");
        material.EnableKeyword("_SHELL_PROTECTOR_FORMAT1");
    }

    public override (int, int) CalculateOffsets(Texture2D texture) {
        int woffset = 13 - (int)Mathf.Log(texture.width, 2) - 1;
        int hoffset = 13 - (int)Mathf.Log(texture.height, 2) - 1;
        return (woffset, hoffset);
    }
} 