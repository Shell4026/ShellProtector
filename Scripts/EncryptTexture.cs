using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor;

namespace Shell.Protector
{
    public class EncryptTexture
    {
        int GetCanMipmapLevel(int w, int h)
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
        bool HasAlpha(Texture2D texture)
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
        public Texture2D[] TextureEncrypt(Texture2D texture, byte[] key, int rounds = 32)
        {
            Texture2D tex = texture;

            if(tex.width % 2 !=0 && tex.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", texture.name);
                return null;
            }

            int mip_lv = GetCanMipmapLevel(tex.width, tex.height);

            Texture2D tmp;
            Texture2D mip = new Texture2D(tex.width, tex.height, TextureFormat.Alpha8, mip_lv, true);
            for (int m = 0; m < mip.mipmapCount; ++m)
            {
                Color32[] pixels_mip = mip.GetPixels32(m);
                for (int i = 0; i < pixels_mip.Length; ++i)
                {
                    pixels_mip[i].r = (byte)(m * 10);
                    pixels_mip[i].g = (byte)(m * 10);
                    pixels_mip[i].b = (byte)(m * 10);
                    pixels_mip[i].a = (byte)(m * 10);
                }
                mip.SetPixels32(pixels_mip, m);
            }

            if (HasAlpha(tex))
            {
                tmp = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, mip_lv - 2, true); //mip_lv-2 is blur trick (like the box filter)
                
                for (int m = 0; m < tmp.mipmapCount; ++m)
                {
                    Color32[] pixels = tex.GetPixels32(m);

                    for (int i = 0; i < pixels.Length; ++i)
                    {
                        byte[] data = new byte[4]
                        {
                        pixels[i].r, pixels[i].g, pixels[i].b, pixels[i].a
                        };
                        byte[] idx = BitConverter.GetBytes(i);

                        key[12] = idx[0];
                        key[13] = idx[1];
                        key[14] = idx[2];
                        key[15] = idx[3];

                        byte[] data_enc = XTEAEncrypt.Encrypt4(data, key, rounds);
                        pixels[i].r = data_enc[0];
                        pixels[i].g = data_enc[1];
                        pixels[i].b = data_enc[2];
                        pixels[i].a = data_enc[3];
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

                    for (int i = 0; i < pixels.Length; ++i)
                    {
                        byte[] data = new byte[3]
                        {
                            pixels[i].r, pixels[i].g, pixels[i].b
                        };
                        byte[] idx = BitConverter.GetBytes(i);

                        key[12] = idx[0];
                        key[13] = idx[1];
                        key[14] = idx[2];
                        key[15] = idx[3];

                        byte[] data_enc = XTEAEncrypt.Encrypt3(data, key, rounds);
                        pixels[i].r = data_enc[0];
                        pixels[i].g = data_enc[1];
                        pixels[i].b = data_enc[2];
                    }
                    tmp.SetPixels32(pixels, m);
                }
                for (int m = 0; m < mip.mipmapCount; ++m)
                {
                    Color32[] pixels_mip = mip.GetPixels32(m);
                    for (int i = 0; i < pixels_mip.Length; ++i)
                    {
                        pixels_mip[i].r = (byte)(m * 10);
                        pixels_mip[i].g = (byte)(m * 10);
                        pixels_mip[i].b = (byte)(m * 10);
                    }
                    mip.SetPixels32(pixels_mip, m);
                }
            }
            mip.filterMode = FilterMode.Point;
            mip.anisoLevel = 0;

            tmp.filterMode = FilterMode.Point;
            tmp.anisoLevel = 0;
            return new Texture2D[] { tmp, mip };
        }
        public Texture2D TextureDecrypt(Texture2D texture, byte[] key, int rounds = 32)
        {
            Texture2D tex = texture;
            Color32[] pixels = tex.GetPixels32();

            Texture2D tmp;
            if (HasAlpha(texture))
            {
                tmp = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, tex.mipmapCount > 1);
                for (int i = 0; i < pixels.Length; ++i)
                {
                    byte[] data = new byte[4]
                    {
                        pixels[i].r, pixels[i].g, pixels[i].b, pixels[i].a
                    };
                    byte[] idx = BitConverter.GetBytes(i);

                    key[12] = idx[0];
                    key[13] = idx[1];
                    key[14] = idx[2];
                    key[15] = idx[3];

                    byte[] data_dec = XTEAEncrypt.Decrypt4(data, key, rounds);
                    pixels[i].r = data_dec[0];
                    pixels[i].g = data_dec[1];
                    pixels[i].b = data_dec[2];
                    pixels[i].a = data_dec[3];
                }
            }
            else
            {
                tmp = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, tex.mipmapCount > 1);
                for (int i = 0; i < pixels.Length; ++i)
                {
                    byte[] data = new byte[3]
                    {
                        pixels[i].r, pixels[i].g, pixels[i].b
                    };
                    byte[] idx = BitConverter.GetBytes(i);

                    key[12] = idx[0];
                    key[13] = idx[1];
                    key[14] = idx[2];
                    key[15] = idx[3];

                    byte[] data_dec = XTEAEncrypt.Decrypt3(data, key, rounds);
                    pixels[i].r = data_dec[0];
                    pixels[i].g = data_dec[1];
                    pixels[i].b = data_dec[2];
                }
            }
            tmp.SetPixels32(pixels);
            return tmp;
        }
    }
}