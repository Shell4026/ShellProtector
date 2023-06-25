using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sh
{
#if UNITY_EDITOR
    public class EncryptTexture : MonoBehaviour
    {
        [SerializeField]
        List<Material> material_list = new List<Material>();
        [SerializeField]
        List<Texture2D> texture_list = new List<Texture2D>();

        public string dir = "Assets/ShellProtect";
        public string pwd = "password";

        [SerializeField]
        int rounds = 32;
        [SerializeField]
        int filter = 1;

        public void Test()
        {
            byte[] data = new byte[8] { 255, 0, 0, 255, 255, 0, 0, 255 };
            byte[] key = MakeKeyBytes(pwd);

            uint pwd1 = (uint)(key[0] + (key[1] << 8) + (key[2] << 16) + (key[3] << 24));
            uint pwd2 = (uint)(key[4] + (key[5] << 8 )+ (key[6] << 16) + (key[7] << 24));
            uint pwd3 = (uint)(key[8] + (key[9] << 8) + (key[10] << 16) + (key[11] << 24));

            string debug_txt = "";
            foreach (var i in key)
                debug_txt += i.ToString() + ' ';
            Debug.Log("Key bytes: " + debug_txt);
            Debug.Log(string.Format("key1:{0}, key2:{1}, key3:{2}", pwd1, pwd2, pwd3));

            debug_txt = "";
            foreach (var i in data)
                debug_txt += i.ToString() + ' ';
            Debug.Log("Data: " + debug_txt);

            debug_txt = "";
            byte[] result = XTEAEncrypt.Encrypt8(data, key);
            foreach (var i in result)
                debug_txt += i.ToString() + ' ';
            Debug.Log("Encrypted data: " + debug_txt);

            debug_txt = "";
            result = XTEAEncrypt.Decrypt8(result, key);
            foreach (var i in result)
                debug_txt += i.ToString() + ' ';
            Debug.Log("Decrypted data: " + debug_txt);
        }

        byte[] MakeKeyBytes(string _key)
        {
            byte[] key = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            byte[] key_bytes = Encoding.ASCII.GetBytes(pwd);

            for (int i = 0; i < key_bytes.Length; ++i)
                key[i] = key_bytes[i];

            return key; 
        }

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

        public Texture2D[] TextureEncrypt(Texture2D texture, bool selection = true)
        {
            byte[] key = MakeKeyBytes(pwd);
            string debug_txt = "";
            foreach (var i in key)
                debug_txt += i.ToString() + ' ';
            Debug.Log("Key bytes: " + debug_txt);

            Texture2D tex = texture;

            if(tex.width % 2 !=0 && tex.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", texture.name);
                return null;
            }

            int mip_lv = GetCanMipmapLevel(tex.width, tex.height);
            Debug.Log(mip_lv);

            Texture2D tmp = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, mip_lv-2, true); //mip_lv-2 is blur trick (look a shader)
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
                //Debug.Log(pixels.Length);
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

            if(!AssetDatabase.IsValidFolder(dir + '/' + gameObject.name))
                AssetDatabase.CreateFolder(dir, gameObject.name);

            AssetDatabase.CreateAsset(tmp, dir + '/' + gameObject.name + '/' + texture.name + "_encrypt.asset");
            AssetDatabase.CreateAsset(mip, dir + '/' + gameObject.name + '/' + texture.name + "_encrypt_mip.asset");
            AssetDatabase.SaveAssets();

            if(selection)
                Selection.activeObject = tmp;
            AssetDatabase.Refresh();
            return new Texture2D[] { tmp, mip };
        }
        public Texture2D TextureDecrypt(Texture2D texture)
        {
            byte[] key = MakeKeyBytes(pwd);
            string debug_txt = "";
            foreach (var i in key)
                debug_txt += i.ToString() + ' ';
            Debug.Log("Key bytes: " + debug_txt);

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

            System.IO.File.WriteAllBytes(dir + texture.name + "_decrypt.png", tmp.EncodeToPNG());
            AssetDatabase.Refresh();
            return tmp;
#endif
        }

        public void Encrypt()
        {
            foreach (var mat in material_list)
            {
                if(Injector.IsSupportShader(mat.shader))
                {
                    if (Injector.IsLockPoiyomi(mat.shader))
                    {
                        if (mat.mainTexture.width % 2 != 0 && mat.mainTexture.height % 2 != 0)
                        {
                            Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", mat.mainTexture.name);
                            continue;
                        }
                        Texture2D[] tex_set = TextureEncrypt((Texture2D)mat.mainTexture, false);

                        Texture2D tex = tex_set[0];
                        Texture2D mip = tex_set[1];

                        mat.mainTexture = tex;
                        mat.SetTexture("_MipTex", mip);

                        Injector injector = new Injector(MakeKeyBytes(pwd), rounds, filter);
                        injector.Inject(mat.shader, dir + "/Decrypt.cginc", tex);
                    }
                    else
                    {
                        Debug.LogError("First, the shader must be locked!");
                    }
                }
            }
        }


    }
}