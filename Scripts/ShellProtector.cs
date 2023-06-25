using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    public class ShellProtector : MonoBehaviour
    {
        [SerializeField]
        List<Material> material_list = new List<Material>();
        [SerializeField]
        List<Texture2D> texture_list = new List<Texture2D>();

        EncryptTexture encrypt = new EncryptTexture();
        Injector injector = new Injector();

        public string dir = "Assets/ShellProtect";
        public string pwd = "password";

        [SerializeField]
        int rounds = 32;
        [SerializeField]
        int filter = 1;

        public byte[] MakeKeyBytes(string _key)
        {
            byte[] key = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            byte[] key_bytes = Encoding.ASCII.GetBytes(pwd);

            for (int i = 0; i < key_bytes.Length; ++i)
                key[i] = key_bytes[i];

            return key;
        }
        public EncryptTexture GetEncryptTexture()
        {
            return encrypt;
        }
        public void Test()
        {
            byte[] data = new byte[8] { 255, 0, 0, 255, 255, 0, 0, 255 };
            byte[] key = MakeKeyBytes(pwd);

            uint pwd1 = (uint)(key[0] + (key[1] << 8) + (key[2] << 16) + (key[3] << 24));
            uint pwd2 = (uint)(key[4] + (key[5] << 8) + (key[6] << 16) + (key[7] << 24));
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
        public void Encrypt()
        {
            string debug_txt = "";
            foreach (var i in MakeKeyBytes(pwd))
                debug_txt += i.ToString() + ' ';
            Debug.Log("Key bytes: " + debug_txt);

            foreach (var mat in material_list)
            {
                byte[] key_bytes = MakeKeyBytes(pwd);
                injector.Init(key_bytes, rounds, filter);
                if (injector.IsSupportShader(mat.shader))
                {
                    if (!Injector.IsLockPoiyomi(mat.shader))
                    {
                        Debug.LogError("First, the shader must be locked!");
                        continue;
                    }

                    if (mat.mainTexture.width % 2 != 0 && mat.mainTexture.height % 2 != 0)
                    {
                        Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", mat.mainTexture.name);
                        continue;
                    }

                    if (injector.WasInjected(mat.shader))
                    {
                        Debug.LogWarning(mat.name + ": The shader is already encrypted.");
                        continue;
                    }

                    try
                    {
                        Texture2D[] tex_set = encrypt.TextureEncrypt((Texture2D)mat.mainTexture, key_bytes, rounds);
                        if (!injector.Inject(mat.shader, dir + "/Decrypt.cginc", tex_set[0]))
                            continue;

                        mat.mainTexture = tex_set[0];
                        mat.SetTexture("_MipTex", tex_set[1]);

                        if (dir[dir.Length - 1] == '/')
                            dir = dir.Remove(dir.Length - 1);

                        if (!AssetDatabase.IsValidFolder(dir + '/' + gameObject.name))
                            AssetDatabase.CreateFolder(dir, gameObject.name);

                        AssetDatabase.CreateAsset(tex_set[0], dir + '/' + gameObject.name + '/' + tex_set[0].name + "_encrypt.asset");
                        AssetDatabase.CreateAsset(tex_set[1], dir + '/' + gameObject.name + '/' + tex_set[1].name + "_encrypt_mip.asset");
                        AssetDatabase.SaveAssets();

                        AssetDatabase.Refresh();
                    }
                    catch (UnityException e)
                    {
                        Debug.LogError(e.Message);
                        continue;
                    }
                }
            }
        }
    }
}