using UnityEngine;
using Shell.Protector;
using System;

public abstract class DXTFormat : BaseTextureFormat {
    protected byte[] GetArrayDXT(byte[] data, int texture_width, int texture_height, bool dxt5, int miplv) {
        int start = 0;
        int end = 0;

        for (int i = 0; i <= miplv; ++i) {
            start += end;
            int w = texture_width / (int)(Mathf.Pow(2, i));
            int h = texture_height / (int)(Mathf.Pow(2, i));
            int block_count = (w / 4) * (h / 4);
            int len = block_count * 8;
            if (dxt5) len *= 2;
            end = len;
        }

        var segment = new ArraySegment<byte>(data, start, end);
        return segment.ToArray();
    }

    protected Texture2D HandleCrunchedFormat(Texture2D texture, int mip_lv, bool isDXT5) {
        if (!texture.name.Contains("Crunched")) return texture;
        
        Debug.LogWarningFormat("{0} is the crunch compression format. There may be degradation in image quality.", texture.name);
        var format = isDXT5 ? TextureFormat.RGBA32 : TextureFormat.RGB24;
        var result = new Texture2D(texture.width, texture.height, format, mip_lv, true);
        
        for (int m = 0; m <= mip_lv; ++m) {
            if (m != 0 && m == mip_lv) break;
            result.SetPixels32(texture.GetPixels32(m), m);
            result.Apply();
        }
        
        result.Compress(isDXT5);
        return result;
    }

    public override void SetFormatKeywords(Material material) {
        material.DisableKeyword("_SHELL_PROTECTOR_FORMAT0");
        material.DisableKeyword("_SHELL_PROTECTOR_FORMAT1");
    }

    public override (int, int) CalculateOffsets(Texture2D texture) {
        int woffset = 13 - (int)Mathf.Log(texture.width, 2) - 1 + 2;
        int hoffset = 13 - (int)Mathf.Log(texture.height, 2) - 1 + 2;
        return (woffset, hoffset);
    }
}

public class DXT1Format : DXTFormat {
    public override bool CanHandle(TextureFormat format) {
        return format == TextureFormat.DXT1 || format == TextureFormat.DXT1Crunched;
    }

    public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm) {
        if (texture.width < 8) {
            Debug.LogErrorFormat("{0} : The texture width must be >= 8px", texture.name);
            return new EncryptResult();
        }
        if (texture.height < 4) {
            Debug.LogErrorFormat("{0} : The texture height must be >= 4px", texture.name);
            return new EncryptResult();
        }

        int mip_lv = GetCanMipmapLevel(texture.width / 4, texture.height / 4);
        Texture2D dxt1 = HandleCrunchedFormat(texture, mip_lv, false);
        
        var result = new EncryptResult();
        if (mip_lv != 0) {
            result.Texture1 = new Texture2D(dxt1.width, dxt1.height, TextureFormat.DXT1, mip_lv, true);
            result.Texture2 = new Texture2D(dxt1.width / 4, dxt1.height / 4, TextureFormat.RGBA32, mip_lv, true);
        } else {
            result.Texture1 = new Texture2D(dxt1.width, dxt1.height, TextureFormat.DXT1, false, true);
            result.Texture2 = new Texture2D(dxt1.width / 4, dxt1.height / 4, TextureFormat.RGBA32, false, true);
        }
        result.Texture2.filterMode = FilterMode.Point;
        result.Texture2.anisoLevel = 0;

        var raw_data = dxt1.GetRawTextureData();
        int lenidx = 0;
        var key_uint = ConvertKeyToUInt(key);

        for (int m = 0; m <= mip_lv; ++m) {
            if (m != 0 && m == mip_lv) break;
            var tex_data = GetArrayDXT(raw_data, dxt1.width, dxt1.height, false, m);
            var pixel = result.Texture2.GetPixels32(m);

            for (int i = 0; i < tex_data.Length; i += 16) {
                key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                key_uint[3] ^= (uint)(i / 8);

                uint[] data = new uint[2];
                data[0] = (uint)(tex_data[i + 0] + (tex_data[i + 1] << 8) + (tex_data[i + 2] << 16) + (tex_data[i + 3] << 24));
                data[1] = (uint)(tex_data[(i + 8) + 0] + (tex_data[(i + 8) + 1] << 8) + (tex_data[(i + 8) + 2] << 16) + (tex_data[(i + 8) + 3] << 24));

                uint[] data_enc = algorithm.Encrypt(data, key_uint);

                for (int j = 0; j < 2; ++j) {
                    pixel[i / 8 + j].r = (byte)((data_enc[j] & 0x000000FF) >> 0);
                    pixel[i / 8 + j].g = (byte)((data_enc[j] & 0x0000FF00) >> 8);
                    pixel[i / 8 + j].b = (byte)((data_enc[j] & 0x00FF0000) >> 16);
                    pixel[i / 8 + j].a = (byte)((data_enc[j] & 0xFF000000) >> 24);
                }
            }
            for (int i = 0; i < tex_data.Length; i += 8) {
                tex_data[i + 0] = 255;
                tex_data[i + 1] = 255;
                tex_data[i + 2] = 0;
                tex_data[i + 3] = 0;
            }
            for (int i = 0; i < tex_data.Length; ++i) {
                raw_data[i + lenidx] = tex_data[i];
            }
            lenidx += tex_data.Length;
            result.Texture2.SetPixels32(pixel, m);
        }
        result.Texture1.LoadRawTextureData(raw_data);
        result.Texture1.filterMode = FilterMode.Point;
        result.Texture1.anisoLevel = 0;

        return result;
    }
}

public class DXT5Format : DXTFormat {
    public override bool CanHandle(TextureFormat format) {
        return format == TextureFormat.DXT5 || format == TextureFormat.DXT5Crunched;
    }

    public override EncryptResult Encrypt(Texture2D texture, byte[] key, IEncryptor algorithm) {
        if (texture.width < 8) {
            Debug.LogErrorFormat("{0} : The texture width must be >= 8px", texture.name);
            return new EncryptResult();
        }
        if (texture.height < 4) {
            Debug.LogErrorFormat("{0} : The texture height must be >= 4px", texture.name);
            return new EncryptResult();
        }

        int mip_lv = GetCanMipmapLevel(texture.width / 4, texture.height / 4);
        Texture2D dxt5 = HandleCrunchedFormat(texture, mip_lv, true);
        
        var result = new EncryptResult();
        if (mip_lv != 0) {
            result.Texture1 = new Texture2D(texture.width, texture.height, TextureFormat.DXT5, mip_lv, true);
            result.Texture2 = new Texture2D(texture.width / 4, texture.height / 4, TextureFormat.RGBA32, mip_lv, true);
        } else {
            result.Texture1 = new Texture2D(texture.width, texture.height, TextureFormat.DXT5, false, true);
            result.Texture2 = new Texture2D(texture.width / 4, texture.height / 4, TextureFormat.RGBA32, false, true);
        }
        result.Texture1.alphaIsTransparency = true;
        result.Texture2.filterMode = FilterMode.Point;
        result.Texture2.anisoLevel = 0;

        var raw_data = dxt5.GetRawTextureData();
        int lenidx = 0;
        var key_uint = ConvertKeyToUInt(key);

        for (int m = 0; m <= mip_lv; ++m) {
            if (m != 0 && m == mip_lv) break;
            var tex_data = GetArrayDXT(raw_data, texture.width, texture.height, true, m);
            var pixel = result.Texture2.GetPixels32(m);

            for (int i = 0; i < tex_data.Length; i += 32) {
                key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                key_uint[3] ^= (uint)(i / 16);

                uint[] data = new uint[2];
                data[0] = (uint)(tex_data[i + 8] + (tex_data[i + 9] << 8) + (tex_data[i + 10] << 16) + (tex_data[i + 11] << 24));
                data[1] = (uint)(tex_data[i + 16 + 8] + (tex_data[i + 16 + 9] << 8) + (tex_data[i + 16 + 10] << 16) + (tex_data[i + 16 + 11] << 24));

                uint[] data_enc = algorithm.Encrypt(data, key_uint);

                for (int j = 0; j < 2; ++j) {
                    pixel[i / 16 + j].r = (byte)((data_enc[j] & 0x000000FF) >> 0);
                    pixel[i / 16 + j].g = (byte)((data_enc[j] & 0x0000FF00) >> 8);
                    pixel[i / 16 + j].b = (byte)((data_enc[j] & 0x00FF0000) >> 16);
                    pixel[i / 16 + j].a = (byte)((data_enc[j] & 0xFF000000) >> 24);
                }
            }
            for (int i = 0; i < tex_data.Length; i += 16) {
                tex_data[i + 8] = 255;
                tex_data[i + 9] = 255;
                tex_data[i + 10] = 0;
                tex_data[i + 11] = 0;
            }
            for (int i = 0; i < tex_data.Length; ++i) {
                raw_data[i + lenidx] = tex_data[i];
            }
            lenidx += tex_data.Length;
            result.Texture2.SetPixels32(pixel, m);
        }
        result.Texture1.LoadRawTextureData(raw_data);
        result.Texture1.filterMode = FilterMode.Point;
        result.Texture1.anisoLevel = 0;

        return result;
    }
} 