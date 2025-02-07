﻿#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEditor;

namespace Shell.Protector
{
    public class EncryptTexture
    {
        public static int GetCanMipmapLevel(int w, int h)
        {
            int w_level, h_level;
            if (w < 1 || h <= 1)
                return 0;
            w_level = (int)Mathf.Log(w, 2);
            h_level = (int)Mathf.Log(h, 2);
            return Math.Max(w_level, h_level);
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
        /// <summary>
        /// 밉 레벨에 해당하는 DXT데이터 배열을 가져오는 함수
        /// </summary>
        private byte[] GetArrayDXT(byte[] data, int texture_width, int texture_height, bool dxt5, int miplv)
        {
            int start = 0;
            int end = 0;

            for (int i = 0; i <= miplv; ++i)
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
        public Texture2D GenerateRefMipmap(int width, int height, bool small = false)
        {
            int mip_lv = GetCanMipmapLevel(width, height);
            Debug.LogFormat("mip {0}, {1} : {2}", width, height, mip_lv);

            Texture2D mip = new Texture2D(width, (small == false) ? height : 1, TextureFormat.RGB24, mip_lv, true);
            mip.filterMode = FilterMode.Bilinear;
            mip.anisoLevel = (small == false) ? 1 : 0;

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
            if (small == false)
                mip.Compress(false);
            return mip;
        }

        public Texture2D GenerateFallback(Texture2D original, int size = 32)
        {
            if(original.width < 128 || original.height < 128)
            {
                return null;
            }
            TextureFormat format = TextureFormat.RGB24;
            bool hasAlpha = HasAlpha(original);
            if (hasAlpha)
                format = TextureFormat.RGBA32;

            RenderTexture renderTexture = new RenderTexture(size, size, 0);
            RenderTexture.active = renderTexture;

            Graphics.Blit(original, renderTexture);

            Texture2D resizedTexture = new Texture2D(size, size, format, true);
            resizedTexture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            resizedTexture.Apply();

            resizedTexture.filterMode = FilterMode.Point;
            resizedTexture.anisoLevel = 0;
            resizedTexture.Compress(false);
            if(hasAlpha)
                resizedTexture.alphaIsTransparency = true;

            RenderTexture.active = null;

            return resizedTexture;
        }

        private Texture2D[] EncryptDXT1(Texture2D texture, byte[] key, IEncryptor encryptor)
        {
            Texture2D[] result = new Texture2D[2];
            uint[] key_uint = new uint[4];
            key_uint[0] = (uint)(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
            key_uint[1] = (uint)(key[4] | (key[5] << 8) | (key[6] << 16) | (key[7] << 24));
            key_uint[2] = (uint)(key[8] | (key[9] << 8) | (key[10] << 16) | (key[11] << 24));
            key_uint[3] = 0;

            int mip_lv = GetCanMipmapLevel(texture.width / 4, texture.height / 4);
            Texture2D dxt1 = texture;
            if (texture.format == TextureFormat.DXT1Crunched)
            {
                Debug.LogWarningFormat("{0} is the crunch compression format. There may be degradation in image quality.", texture.name);
                dxt1 = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, mip_lv, true);
                for (int m = 0; m <= mip_lv; ++m)
                {
                    if (m != 0 && m == mip_lv)
                        break;
                    dxt1.SetPixels32(texture.GetPixels32(m), m);
                    dxt1.Apply();
                }
                dxt1.Compress(false);
            }
            if (mip_lv != 0)
            {
                result[0] = new Texture2D(dxt1.width, dxt1.height, TextureFormat.DXT1, mip_lv, true);
                result[1] = new Texture2D(dxt1.width / 4, dxt1.height / 4, TextureFormat.RGBA32, mip_lv, true);
            }
            else
            {
                result[0] = new Texture2D(dxt1.width, dxt1.height, TextureFormat.DXT1, false, true);
                result[1] = new Texture2D(dxt1.width / 4, dxt1.height / 4, TextureFormat.RGBA32, false, true);
            }
            result[1].filterMode = FilterMode.Point;
            result[1].anisoLevel = 0;
            //Note: DXT1 per 4x4 block is 64bit
            var raw_data = dxt1.GetRawTextureData();
            int lenidx = 0;

            for (int m = 0; m <= mip_lv; ++m)
            {
                if (m != 0 && m == mip_lv)
                    break;
                var tex_data = GetArrayDXT(raw_data, dxt1.width, dxt1.height, false, m);
                var pixel = result[1].GetPixels32(m);

                for (int i = 0; i < tex_data.Length; i += 16) //reference color texture
                {
                    key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                    key_uint[3] ^= (uint)(i / 8); //8 bytes(1block) are same id.

                    uint[] data = new uint[2];
                    data[0] = (uint)(tex_data[i + 0] + (tex_data[i + 1] << 8) + (tex_data[i + 2] << 16) + (tex_data[i + 3] << 24));
                    data[1] = (uint)(tex_data[(i + 8) + 0] + (tex_data[(i + 8) + 1] << 8) + (tex_data[(i + 8) + 2] << 16) + (tex_data[(i + 8) + 3] << 24));

                    uint[] data_enc = encryptor.Encrypt(data, key_uint);

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
                result[1].SetPixels32(pixel, m);
            }
            result[0].LoadRawTextureData(raw_data);

            return result;
        }
        private Texture2D[] EncryptDXT5(Texture2D tex, byte[] key, IEncryptor encryptor)
        {
            Texture2D[] result = new Texture2D[2];
            uint[] key_uint = new uint[4];
            key_uint[0] = (uint)(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
            key_uint[1] = (uint)(key[4] | (key[5] << 8) | (key[6] << 16) | (key[7] << 24));
            key_uint[2] = (uint)(key[8] | (key[9] << 8) | (key[10] << 16) | (key[11] << 24));
            key_uint[3] = 0;

            int mip_lv = GetCanMipmapLevel(tex.width / 4, tex.height / 4);
            Texture2D dxt5 = tex;
            if (tex.format == TextureFormat.DXT5Crunched)
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

            if (mip_lv != 0)
            {
                result[0] = new Texture2D(tex.width, tex.height, TextureFormat.DXT5, mip_lv, true);
                result[1] = new Texture2D(tex.width / 4, tex.height / 4, TextureFormat.RGBA32, mip_lv, true);
            }
            else
            {
                result[0] = new Texture2D(tex.width, tex.height, TextureFormat.DXT5, false, true);
                result[1] = new Texture2D(tex.width / 4, tex.height / 4, TextureFormat.RGBA32, false, true);
            }
            result[0].alphaIsTransparency = true;
            result[1].filterMode = FilterMode.Point;
            result[1].anisoLevel = 0;
            var raw_data = dxt5.GetRawTextureData();
            int lenidx = 0;
            for (int m = 0; m <= mip_lv; ++m)
            {
                if (m != 0 && m == mip_lv)
                    break;
                var tex_data = GetArrayDXT(raw_data, tex.width, tex.height, true, m);
                var pixel = result[1].GetPixels32(m);

                for (int i = 0; i < tex_data.Length; i += 32) //reference color texture
                {
                    key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                    key_uint[3] ^= (uint)(i / 16);

                    uint[] data = new uint[2];
                    data[0] = (uint)(tex_data[i + 8] + (tex_data[i + 9] << 8) + (tex_data[i + 10] << 16) + (tex_data[i + 11] << 24));
                    data[1] = (uint)(tex_data[i + 16 + 8] + (tex_data[i + 16 + 9] << 8) + (tex_data[i + 16 + 10] << 16) + (tex_data[i + 16 + 11] << 24));

                    uint[] data_enc = encryptor.Encrypt(data, key_uint);

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
                result[1].SetPixels32(pixel, m);
            }
            result[0].LoadRawTextureData(raw_data);

            return result;
        }
        private Texture2D[] EncryptRGB24(Texture2D tex, byte[] key, IEncryptor encryptor)
        {
            Texture2D[] result = new Texture2D[2];
            uint[] key_uint = new uint[4];
            key_uint[0] = (uint)(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
            key_uint[1] = (uint)(key[4] | (key[5] << 8) | (key[6] << 16) | (key[7] << 24));
            key_uint[2] = (uint)(key[8] | (key[9] << 8) | (key[10] << 16) | (key[11] << 24));
            key_uint[3] = 0;

            int mip_lv = GetCanMipmapLevel(tex.width, tex.height);
            result[0] = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, mip_lv - 2, true); //mip_lv-2 is blur trick (like the box filter)
            for (int m = 0; m < result[0].mipmapCount; ++m)
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

                    uint[] data_enc = encryptor.Encrypt(data, key_uint);

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
                result[0].SetPixels32(pixels, m);
            }
            return result;
        }

        private Texture2D[] EncryptRGBA32(Texture2D tex, byte[] key, IEncryptor encryptor)
        {
            Texture2D[] result = new Texture2D[2];
            uint[] key_uint = new uint[4];
            key_uint[0] = (uint)(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
            key_uint[1] = (uint)(key[4] | (key[5] << 8) | (key[6] << 16) | (key[7] << 24));
            key_uint[2] = (uint)(key[8] | (key[9] << 8) | (key[10] << 16) | (key[11] << 24));
            key_uint[3] = 0;

            int mip_lv = GetCanMipmapLevel(tex.width, tex.height);
            result[0] = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, mip_lv - 2, true); //mip_lv-2 is blur trick (like the box filter)
            for (int m = 0; m < result[0].mipmapCount; ++m)
            {
                Color32[] pixels = tex.GetPixels32(m);

                for (int i = 0; i < pixels.Length; i += 2)
                {
                    key_uint[3] = (uint)(key[12] | (key[13] << 8) | (key[14] << 16) | (key[15] << 24));
                    key_uint[3] ^= (uint)i;

                    uint[] data = new uint[2];
                    data[0] = (uint)(pixels[i + 0].r + (pixels[i + 0].g << 8) + (pixels[i + 0].b << 16) + (pixels[i + 0].a << 24));
                    data[1] = (uint)(pixels[i + 1].r + (pixels[i + 1].g << 8) + (pixels[i + 1].b << 16) + (pixels[i + 1].a << 24));

                    uint[] data_enc = encryptor.Encrypt(data, key_uint);

                    pixels[i + 0].r = (byte)((data_enc[0] & 0x000000FF) >> 0);
                    pixels[i + 0].g = (byte)((data_enc[0] & 0x0000FF00) >> 8);
                    pixels[i + 0].b = (byte)((data_enc[0] & 0x00FF0000) >> 16);
                    pixels[i + 0].a = (byte)((data_enc[0] & 0xFF000000) >> 24);
                    pixels[i + 1].r = (byte)((data_enc[1] & 0x000000FF) >> 0);
                    pixels[i + 1].g = (byte)((data_enc[1] & 0x0000FF00) >> 8);
                    pixels[i + 1].b = (byte)((data_enc[1] & 0x00FF0000) >> 16);
                    pixels[i + 1].a = (byte)((data_enc[1] & 0xFF000000) >> 24);
                }
                result[0].SetPixels32(pixels, m);
            }

            return result;
        }

        public Texture2D[] TextureEncrypt(Texture2D texture, byte[] key, IEncryptor encryptor)
        {
            Texture2D tex = texture;
            if (tex.width % 2 != 0 && tex.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", texture.name);
                return null;
            }

            Texture2D[] result = new Texture2D[2];

            if (texture.format == TextureFormat.DXT1 || 
                texture.format == TextureFormat.DXT1Crunched ||
                texture.format == TextureFormat.DXT5 ||
                texture.format == TextureFormat.DXT5Crunched)
            {
                if (tex.width < 8)
                {
                    Debug.LogErrorFormat("{0} : The texture width must be >= 8px", texture.name);
                    return null;
                }
                if (tex.height < 4)
                {
                    Debug.LogErrorFormat("{0} : The texture height must be >= 4px", texture.name);
                    return null;
                }
            }

            if (texture.format == TextureFormat.DXT1 || texture.format == TextureFormat.DXT1Crunched)
            {
                result = EncryptDXT1(texture, key, encryptor);
            }
            else if (texture.format == TextureFormat.DXT5 || texture.format == TextureFormat.DXT5Crunched)
            {
                result = EncryptDXT5(texture, key, encryptor);
            }
            else if (tex.format == TextureFormat.RGB24)
            {
                result = EncryptRGB24(tex, key, encryptor);
            }
            else if (tex.format == TextureFormat.RGBA32)
            {
                result = EncryptRGBA32(tex, key, encryptor);
            }
            else
            {
                Debug.LogErrorFormat("{0} is not supported texture format! supported type:DXT1, DXT5, RGB, RGBA", tex.name);
                result[0] = null;
            }
            if (result[0])
            {
                result[0].filterMode = FilterMode.Point;
                result[0].anisoLevel = 0;
            }
            return result;
        }
        /*public Texture2D TextureDecryptXXTEA(Texture2D texture, byte[] key)
        {
            Texture2D tex = texture;

            if (tex.width % 2 != 0 && tex.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", texture.name);
                return null;
            }

            int mip_lv = GetCanMipmapLevel(tex.width, tex.height);

            Texture2D tmp;

            XXTEA xxtea = new XXTEA();

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

                        uint[] data_enc = xxtea.Decrypt(data, key_uint);

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

                        uint[] data_enc = xxtea.Decrypt(data, key_uint);

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
        }*/
    }
}
#endif