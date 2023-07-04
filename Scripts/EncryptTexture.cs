using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor;

namespace Shell.Protector
{
    public class EncryptTexture
    {
        public int GetCanMipmapLevel(int w, int h)
        {
            int w_level = 0, h_level = 0;
            while(w != 1)
            {
                w /= 2;
                ++w_level;
            }
            while (h != 1)
            {
                h /= 2;
                ++h_level;
            }
            return Math.Min(w_level, h_level) + 1;
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
        public Texture2D GenerateRefMipmap(int width, int height)
        {
            int mip_lv = GetCanMipmapLevel(width, height);

            Texture2D mip = new Texture2D(width, height, TextureFormat.RGB24, mip_lv, true);
            mip.filterMode = FilterMode.Point;
            mip.anisoLevel = 0;

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
        public Texture2D TextureEncryptXXTEA(Texture2D texture, byte[] key)
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
            key_uint[0] = (uint)(key[0] + (key[1] << 8) + (key[2] << 16) + (key[3] << 24));
            key_uint[1] = (uint)(key[4] + (key[5] << 8) + (key[6] << 16) + (key[7] << 24));
            key_uint[2] = (uint)(key[8] + (key[9] << 8) + (key[10] << 16) + (key[11] << 24));
            if (HasAlpha(tex))
            {
                tmp = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, mip_lv - 2, true); //mip_lv-2 is blur trick (like the box filter)
                for (int m = 0; m < tmp.mipmapCount; ++m)
                {
                    Color32[] pixels = tex.GetPixels32(m);

                    for (int i = 0; i < pixels.Length; i += 2)
                    {
                        key_uint[3] = (uint)i;

                        uint[] data = new uint[2];
                        data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 0].a << 24));
                        data[1] = (uint)(pixels[i + 1].r + (pixels[i + 1].g << 8) + (pixels[i + 1].b << 16) + (pixels[i + 1].a << 24));

                        uint[] data_enc = XXTEA.Encrypt(data, key_uint);

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
                        key_uint[3] = (uint)i;

                        uint[] data = new uint[3];
                        data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 1].r << 24));
                        data[1] = (uint)(pixels[i + 1].g + (pixels[i + 1].b << 8) + (pixels[i + 2].r << 16) + (pixels[i + 2].g << 24));
                        data[2] = (uint)(pixels[i + 2].b + (pixels[i + 3].r << 8) + (pixels[i + 3].g << 16) + (pixels[i + 3].b << 24));

                        uint[] data_enc = XXTEA.Encrypt(data, key_uint);

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
            key_uint[0] = (uint)(key[0] + (key[1] << 8) + (key[2] << 16) + (key[3] << 24));
            key_uint[1] = (uint)(key[4] + (key[5] << 8) + (key[6] << 16) + (key[7] << 24));
            key_uint[2] = (uint)(key[8] + (key[9] << 8) + (key[10] << 16) + (key[11] << 24));
            if (HasAlpha(tex))
            {
                tmp = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, mip_lv - 2, true); //mip_lv-2 is blur trick (like the box filter)
                for (int m = 0; m < tmp.mipmapCount; ++m)
                {
                    Color32[] pixels = tex.GetPixels32(m);

                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        key_uint[3] = (uint)i;
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
                        key_uint[3] = (uint)i;

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