#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace Shell.Protector
{
    public class EncryptTexture
    {
        public int GetCanMipmapLevel(int w, int h, bool dxt = false)
        {
            int w_level = 0, h_level = 0;
            int min_size = dxt ? 8 : 1;
            if (w < min_size || h < min_size)
                return 0;
            while(w != min_size)
            {
                w /= 2;
                if (w < 0)
                    break;
                ++w_level;
            }
            while (h != min_size)
            {
                h /= 2;
                if (h < 0)
                    break;
                ++h_level;
            }
            return Math.Min(w_level, h_level);
        }
        public int GetDXT1Length(int w, int h, int m)
        {
            int len = 0;
            for (int i = 0; i < m + 1; ++i)
            {
                len += w * h / 16 * 8;
                w /= 2;
                h /= 2;
            }
            return len;
        }
        public static bool HasAlpha(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a != 255)
                {
                    return true;
                }
            }
            return false;
        }
        private int GetArrayIdxInBlock(int block, int n, int w)
        {
            int wblock = w / 4;
            int x = (block % wblock);
            int y = block / wblock;

            int pivot = x * 4 + y * w * 4;

            return pivot + n / 4 * w + n % 4;
        }
        private byte[] GetArrayDXT(byte[] data, int texture_width, int texture_height, bool dxt5, int miplv)
        {
            int start = 0;
            int end = 0;

            for(int i = 0; i < miplv + 1; ++i)
            {
                start += end;
                int w = texture_width / (int)(Mathf.Pow(2, i));
                int h = texture_height / (int)(Mathf.Pow(2, i));
                int block_count = (w / 4) * (h / 4);
                int len = block_count * 8;
                if (dxt5)
                    len *= 2;
                end = len;
            }

            var segment = new ArraySegment<byte>(data, start, end);
            return segment.ToArray();
        }
        public Texture2D GenerateRefMipmap(int width, int height)
        {
            int mip_lv = GetCanMipmapLevel(width, height);

            Texture2D mip = new Texture2D(width, height, TextureFormat.RGB24, mip_lv, true);
            mip.filterMode = FilterMode.Bilinear;
            mip.anisoLevel = 1;

            for (int m = 0; m < mip.mipmapCount; ++m)
            {
                Color32[] pixels_mip = mip.GetPixels32(m);
                for (int i = 0; i < pixels_mip.Length; ++i)
                {
                    pixels_mip[i].r = (byte)(m * 10);
                    pixels_mip[i].g = 0;
                    pixels_mip[i].b = 0;
                }
                mip.SetPixels32(pixels_mip, m);
            }
            mip.Compress(false);
            return mip;
        }
        public Texture2D[] TextureEncryptXXTEA(Texture2D texture, byte[] key, uint rounds = 25)
        {
            Texture2D tex = texture;

            if (tex.width % 2 != 0 && tex.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", texture.name);
                return null;
            }

            Texture2D[] tmp = new Texture2D[2];

            uint[] key_uint = new uint[4];
            key_uint[0] = (uint)(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
            key_uint[1] = (uint)(key[4] | (key[5] << 8) | (key[6] << 16) | (key[7] << 24));
            key_uint[2] = (uint)(key[8] | (key[9] << 8) | (key[10] << 16) | (key[11] << 24));
            key_uint[3] = 0;

            if (texture.format == TextureFormat.DXT1 || texture.format == TextureFormat.DXT1Crunched)
            {
                int mip_lv = GetCanMipmapLevel(tex.width, tex.height, true);
                Texture2D dxt1 = tex;
                if (texture.format == TextureFormat.DXT1Crunched)
                {
                    Debug.LogWarningFormat("{0} is the crunch compression format. There may be degradation in image quality.", tex.name);
                    dxt1 = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, mip_lv, true);
                    for (int m = 0; m <= mip_lv; ++m)
                    {
                        if (m != 0 && m == mip_lv)
                            break;
                        dxt1.SetPixels32(tex.GetPixels32(m), m);
                        dxt1.Apply();
                    }
                    dxt1.Compress(false);
                }
                mip_lv = GetCanMipmapLevel(tex.width, tex.height, true);
                if (mip_lv != 0)
                {
                    tmp[0] = new Texture2D(tex.width, tex.height, TextureFormat.DXT1, mip_lv, true);
                    tmp[1] = new Texture2D(tex.width / 4, tex.height / 4, TextureFormat.RGBA32, mip_lv, true);
                }
                else
                {
                    tmp[0] = new Texture2D(tex.width, tex.height, TextureFormat.DXT1, false, true);
                    tmp[1] = new Texture2D(tex.width / 4, tex.height / 4, TextureFormat.RGBA32, false, true);
                }
                tmp[1].filterMode = FilterMode.Point;
                tmp[1].anisoLevel = 0;
                var raw_data = dxt1.GetRawTextureData();

                int lenidx = 0;
                for (int m = 0; m <= mip_lv; ++m)
                {
                    if (m != 0 && m == mip_lv)
                        break;
                    var tex_data = GetArrayDXT(raw_data, tex.width, tex.height, false, m);
                    var pixel = tmp[1].GetPixels32(m);
                    //Debug.Log(m + "=" + tex_data.Length);

                    for (int i = 0; i < tex_data.Length; i += 16) //reference color texture
                    {
                        key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                        key_uint[3] ^= (uint)(i / 8);

                        uint[] data = new uint[2];
                        data[0] = (uint)(tex_data[i + 0] + (tex_data[i + 1] << 8) + (tex_data[i + 2] << 16) + (tex_data[i + 3] << 24));
                        data[1] = (uint)(tex_data[(i + 8) + 0] + (tex_data[(i + 8) + 1] << 8) + (tex_data[(i + 8) + 2] << 16) + (tex_data[(i + 8) + 3] << 24));

                        uint[] data_enc = XXTEA.Encrypt(data, key_uint, rounds);

                        for (int j = 0; j < 2; ++j)
                        {
                            pixel[i / 8 + j].r = (byte)((data_enc[j] & 0x000000FF) >> 0);
                            pixel[i / 8 + j].g = (byte)((data_enc[j] & 0x0000FF00) >> 8);
                            pixel[i / 8 + j].b = (byte)((data_enc[j] & 0x00FF0000) >> 16);
                            pixel[i / 8 + j].a = (byte)((data_enc[j] & 0xFF000000) >> 24);
                        }
                    }
                    for (int i = 0; i < tex_data.Length; i += 8)
                    {
                        tex_data[i + 0] = 255;
                        tex_data[i + 1] = 255;
                        tex_data[i + 2] = 0;
                        tex_data[i + 3] = 0;
                    }
                    for (int i = 0; i < tex_data.Length; ++i)
                    {
                        raw_data[i + lenidx] = tex_data[i];
                    }
                    lenidx += tex_data.Length;
                    tmp[1].SetPixels32(pixel, m);
                }
                tmp[0].LoadRawTextureData(raw_data);
            }
            else if (texture.format == TextureFormat.DXT5 || texture.format == TextureFormat.DXT5Crunched)
            {
                int mip_lv = GetCanMipmapLevel(tex.width, tex.height, true);
                Texture2D dxt5 = tex;
                if (texture.format == TextureFormat.DXT5Crunched)
                {
                    Debug.LogWarningFormat("{0} is the crunch compression format. There may be degradation in image quality.", tex.name);
                    dxt5 = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, mip_lv, true);
                    for (int m = 0; m < mip_lv; ++m)
                    {
                        if (m != 0 && m == mip_lv)
                            break;
                        dxt5.SetPixels32(tex.GetPixels32(m), m);
                        dxt5.Apply();
                    }

                    dxt5.Compress(true);
                }
                mip_lv = GetCanMipmapLevel(tex.width, tex.height, true);

                if (mip_lv != 0)
                {
                    tmp[0] = new Texture2D(tex.width, tex.height, TextureFormat.DXT5, mip_lv, true);
                    tmp[1] = new Texture2D(tex.width / 4, tex.height / 4, TextureFormat.RGBA32, mip_lv, true);
                }
                else
                {
                    tmp[0] = new Texture2D(tex.width, tex.height, TextureFormat.DXT5, false, true);
                    tmp[1] = new Texture2D(tex.width / 4, tex.height / 4, TextureFormat.RGBA32, false, true);
                }
                tmp[0].alphaIsTransparency = true;
                tmp[1].filterMode = FilterMode.Point;
                tmp[1].anisoLevel = 0;
                var raw_data = dxt5.GetRawTextureData();
                int lenidx = 0;
                for (int m = 0; m <= mip_lv; ++m)
                {
                    if (m != 0 && m == mip_lv)
                        break;
                    var tex_data = GetArrayDXT(raw_data, tex.width, tex.height, true, m);
                    var pixel = tmp[1].GetPixels32(m);

                    for (int i = 0; i < tex_data.Length; i += 32) //reference color texture
                    {
                        key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                        key_uint[3] ^= (uint)(i / 16);

                        uint[] data = new uint[2];
                        data[0] = (uint)(tex_data[i + 8] + (tex_data[i + 9] << 8) + (tex_data[i + 10] << 16) + (tex_data[i + 11] << 24));
                        data[1] = (uint)(tex_data[i + 16 + 8] + (tex_data[i + 16 + 9] << 8) + (tex_data[i + 16 + 10] << 16) + (tex_data[i + 16 + 11] << 24));

                        uint[] data_enc = XXTEA.Encrypt(data, key_uint, rounds);

                        for (int j = 0; j < 2; ++j)
                        {
                            pixel[i / 16 + j].r = (byte)((data_enc[j] & 0x000000FF) >> 0);
                            pixel[i / 16 + j].g = (byte)((data_enc[j] & 0x0000FF00) >> 8);
                            pixel[i / 16 + j].b = (byte)((data_enc[j] & 0x00FF0000) >> 16);
                            pixel[i / 16 + j].a = (byte)((data_enc[j] & 0xFF000000) >> 24);
                        }
                    }
                    for (int i = 0; i < tex_data.Length; i += 16)
                    {
                        tex_data[i + 8] = 255;
                        tex_data[i + 9] = 255;
                        tex_data[i + 10] = 0;
                        tex_data[i + 11] = 0;
                    }
                    for (int i = 0; i < tex_data.Length; ++i)
                    {
                        raw_data[i + lenidx] = tex_data[i];
                    }
                    lenidx += tex_data.Length;
                    tmp[1].SetPixels32(pixel, m);
                }
                tmp[0].LoadRawTextureData(raw_data);
            }
            else if (tex.format == TextureFormat.RGBA32)
            {
                int mip_lv = GetCanMipmapLevel(tex.width, tex.height, false);
                tmp[0] = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, mip_lv - 2, true); //mip_lv-2 is blur trick (like the box filter)
                for (int m = 0; m < tmp[0].mipmapCount; ++m)
                {
                    Color32[] pixels = tex.GetPixels32(m);

                    for (int i = 0; i < pixels.Length; i += 2)
                    {
                        key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                        key_uint[3] ^= (uint)i;

                        uint[] data = new uint[2];
                        data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 0].a << 24));
                        data[1] = (uint)(pixels[i + 1].r + (pixels[i + 1].g << 8) + (pixels[i + 1].b << 16) + (pixels[i + 1].a << 24));

                        uint[] data_enc = XXTEA.Encrypt(data, key_uint, rounds);

                        pixels[i + 0].r = (byte)((data_enc[0] & 0x000000FF) >> 0);
                        pixels[i + 0].g = (byte)((data_enc[0] & 0x0000FF00) >> 8);
                        pixels[i + 0].b = (byte)((data_enc[0] & 0x00FF0000) >> 16);
                        pixels[i + 0].a = (byte)((data_enc[0] & 0xFF000000) >> 24);
                        pixels[i + 1].r = (byte)((data_enc[1] & 0x000000FF) >> 0);
                        pixels[i + 1].g = (byte)((data_enc[1] & 0x0000FF00) >> 8);
                        pixels[i + 1].b = (byte)((data_enc[1] & 0x00FF0000) >> 16);
                        pixels[i + 1].a = (byte)((data_enc[1] & 0xFF000000) >> 24);
                    }
                    tmp[0].SetPixels32(pixels, m);
                }
            }
            else if (tex.format == TextureFormat.RGB24)
            {
                int mip_lv = GetCanMipmapLevel(tex.width, tex.height, false);
                tmp[0] = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, mip_lv - 2, true); //mip_lv-2 is blur trick (like the box filter)
                for (int m = 0; m < tmp[0].mipmapCount; ++m)
                {
                    Color32[] pixels = tex.GetPixels32(m);

                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                        key_uint[3] ^= (uint)i;

                        uint[] data = new uint[3];
                        data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 1].r << 24));
                        data[1] = (uint)(pixels[i + 1].g + (pixels[i + 1].b << 8) + (pixels[i + 2].r << 16) + (pixels[i + 2].g << 24));
                        data[2] = (uint)(pixels[i + 2].b + (pixels[i + 3].r << 8) + (pixels[i + 3].g << 16) + (pixels[i + 3].b << 24));

                        uint[] data_enc = XXTEA.Encrypt(data, key_uint, rounds);

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
                    tmp[0].SetPixels32(pixels, m);
                }
            }
            else
            {
                Debug.LogErrorFormat("{0} is not supported texture format! supported type:DXT1, DXT5, RGB, RGBA", tex.name);
                tmp[0] = null;
            }
            if (tmp[0])
            {
                tmp[0].filterMode = FilterMode.Point;
                tmp[0].anisoLevel = 0;
            }
            return tmp;
        }
        public Texture2D TextureDecryptXXTEA(Texture2D texture, byte[] key)
        {
            Texture2D tex = texture;

            if (tex.width % 2 != 0 && tex.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", texture.name);
                return null;
            }

            int mip_lv = GetCanMipmapLevel(tex.width, tex.height);

            Texture2D tmp;

            uint[] key_uint = new uint[4];
            key_uint[0] = (uint)(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
            key_uint[1] = (uint)(key[4] | (key[5] << 8) | (key[6] << 16) | (key[7] << 24));
            key_uint[2] = (uint)(key[8] | (key[9] << 8) | (key[10] << 16) | (key[11] << 24));
            key_uint[3] = 0;
            if (HasAlpha(tex))
            {
                tmp = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, mip_lv - 2, true); //mip_lv-2 is blur trick (like the box filter)
                for (int m = 0; m < tmp.mipmapCount; ++m)
                {
                    Color32[] pixels = tex.GetPixels32(m);

                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                        key_uint[3] ^= (uint)i;

                        uint[] data = new uint[2];
                        data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 0].a << 24));
                        data[1] = (uint)(pixels[i + 1].r + (pixels[i + 1].g << 8) + (pixels[i + 1].b << 16) + (pixels[i + 1].a << 24));

                        uint[] data_enc = XXTEA.Decrypt(data, key_uint);

                        pixels[i + 0].r = (byte)((data_enc[0] & 0x000000FF) >> 0);
                        pixels[i + 0].g = (byte)((data_enc[0] & 0x0000FF00) >> 8);
                        pixels[i + 0].b = (byte)((data_enc[0] & 0x00FF0000) >> 16);
                        pixels[i + 0].a = (byte)((data_enc[0] & 0xFF000000) >> 24);
                        pixels[i + 1].r = (byte)((data_enc[1] & 0x000000FF) >> 0);
                        pixels[i + 1].g = (byte)((data_enc[1] & 0x0000FF00) >> 8);
                        pixels[i + 1].b = (byte)((data_enc[1] & 0x00FF0000) >> 16);
                        pixels[i + 1].a = (byte)((data_enc[1] & 0xFF000000) >> 24);
                    }
                    tmp.SetPixels32(pixels, m);
                }
            }
            else
            {
                tmp = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, mip_lv - 2, true); //mip_lv-2 is blur trick (like the box filter)
                for (int m = 0; m < tmp.mipmapCount; ++m)
                {
                    Color32[] pixels = tex.GetPixels32(m);

                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                        key_uint[3] ^= (uint)i;

                        uint[] data = new uint[3];
                        data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 1].r << 24));
                        data[1] = (uint)(pixels[i + 1].g + (pixels[i + 1].b << 8) + (pixels[i + 2].r << 16) + (pixels[i + 2].g << 24));
                        data[2] = (uint)(pixels[i + 2].b + (pixels[i + 3].r << 8) + (pixels[i + 3].g << 16) + (pixels[i + 3].b << 24));

                        uint[] data_enc = XXTEA.Decrypt(data, key_uint);

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
                    tmp.SetPixels32(pixels, m);
                }
            }

            tmp.filterMode = FilterMode.Point;
            tmp.anisoLevel = 0;
            return tmp;
        }
    }
}
#endif