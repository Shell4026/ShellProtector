using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shell.Protector
{
#if UNITY_EDITOR
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
        public Texture2D[] TextureEncrypt(Texture2D texture, byte[] key, int rounds = 32)
        {
            Texture2D tex = texture;

            if(tex.width % 2 !=0 && tex.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", texture.name);
                return null;
            }

            int mip_lv = GetCanMipmapLevel(tex.width, tex.height);

            Texture2D tmp = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, mip_lv-2, true); //mip_lv-2 is blur trick (like the box filter)
            Texture2D mip = new Texture2D(tex.width, tex.height, TextureFormat.Alpha8, mip_lv, true);
            for (int m = 0; m < tmp.mipmapCount; ++m)
            {
                Color32[] pixels = tex.GetPixels32(m);

                for (int i = 0; i < pixels.Length; ++i)
                {
                    byte[] data = new byte[4] 
                    { 
                        pixels[i].r, pixels[i].g, pixels[i].b, pixels[i].a
                    }; //4byte
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
            for (int m = 0; m < mip.mipmapCount; ++m)
            {
                Color32[] pixels_mip = mip.GetPixels32(m);
                for (int i = 0; i < pixels_mip.Length; ++i)
                {
                    pixels_mip[i].a = (byte)(m * 10);
                }
                mip.SetPixels32(pixels_mip, m);
            }
            tmp.filterMode = FilterMode.Point;
            mip.filterMode = FilterMode.Point;
            
            tmp.anisoLevel = 0;
            mip.anisoLevel = 0;

            return new Texture2D[] { tmp, mip };
        }
        public Texture2D TextureDecrypt(Texture2D texture, byte[] key, int rounds = 32)
        {
            Texture2D tex = texture;
            Texture2D tmp = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, tex.mipmapCount > 1);

            Color32[] pixels = tex.GetPixels32();
            Debug.Log(tex.mipmapCount);
            Color32[] pixels2 = tex.GetPixels32(tex.mipmapCount - 1);
            Debug.Log(pixels.Length);
            for (int i = 0; i < pixels.Length; ++i)
            {
                byte[] data = new byte[4]
                {
                        pixels[i].r, pixels[i].g, pixels[i].b, pixels[i].a
                }; //4byte
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

            tmp.SetPixels32(pixels);
            return tmp;
#endif
        }
    }
}