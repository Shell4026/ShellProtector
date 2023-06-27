using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
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
            byte[] key_bytes = Encoding.ASCII.GetBytes(_key);

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
            byte[] data = new byte[4] { 255, 250, 245, 240 };
            byte[] key = MakeKeyBytes(pwd);

            uint pwd1 = (uint)(key[0] + (key[1] << 8) + (key[2] << 16) + (key[3] << 24));
            uint pwd2 = (uint)(key[4] + (key[5] << 8) + (key[6] << 16) + (key[7] << 24));
            uint pwd3 = (uint)(key[8] + (key[9] << 8) + (key[10] << 16) + (key[11] << 24));

            Debug.Log("Key bytes: " + string.Join(", ", key));
            Debug.Log(string.Format("key1:{0}, key2:{1}, key3:{2}", pwd1, pwd2, pwd3));
            Debug.Log("Data: " + string.Join(", ", data));

            byte[] result = XTEAEncrypt.Encrypt(data, key);
            Debug.Log("Encrypted data: " + string.Join(", ", result));

            result = XTEAEncrypt.Decrypt(result, key);
            Debug.Log("Decrypted data: " + string.Join(", ", result));
        }
        public void Test2()
        {
            byte[] data_byte = new byte[12] { 255, 250, 245, 240, 235, 230, 225, 220, 215, 210, 205, 200 };
            byte[] key_byte = MakeKeyBytes(pwd);

            uint[] data = new uint[3];
            data[0] = (uint)(data_byte[0] + (data_byte[1] << 8) + (data_byte[2] << 16) + (data_byte[3] << 24));
            data[1] = (uint)(data_byte[4] + (data_byte[5] << 8) + (data_byte[6] << 16) + (data_byte[7] << 24));
            data[2] = (uint)(data_byte[8] + (data_byte[9] << 8) + (data_byte[10] << 16) + (data_byte[11] << 24));

            uint[] key = new uint[4];
            key[0] = (uint)(key_byte[0] + (key_byte[1] << 8) + (key_byte[2] << 16) + (key_byte[3] << 24));
            key[1] = (uint)(key_byte[4] + (key_byte[5] << 8) + (key_byte[6] << 16) + (key_byte[7] << 24));
            key[2] = (uint)(key_byte[8] + (key_byte[9] << 8) + (key_byte[10] << 16) + (key_byte[11] << 24));
            key[3] = 0;

            Debug.Log("Key bytes: " + string.Join(", ", key_byte));
            Debug.Log(string.Format("key1:{0}, key2:{1}, key3:{2}", key[0], key[1], key[2]));
            Debug.Log("Data: " + string.Join(", ", data));

            uint[] result = XXTEA.Encrypt(data, key);
            Debug.Log("Encrypted data: " + string.Join(", ", result));

            result = XXTEA.Decrypt(result, key);
            Debug.Log("Decrypted data: " + string.Join(", ", result));
        }
        public void SetRWEnableTexture(Texture2D texture)
        {
            if (texture.isReadable)
                return;
            string path = AssetDatabase.GetAssetPath(texture);
            string meta = File.ReadAllText(path + ".meta");

            meta = Regex.Replace(meta, "isReadable: 0", "isReadable: 1");
            File.WriteAllText(path + ".meta", meta);

            AssetDatabase.Refresh();
        }
        public GameObject DuplicateAvatar(GameObject avatar)
        {
            GameObject cpy = Instantiate(avatar);
            cpy.name = avatar.name + "_encrypted";
            return cpy;
        }
        public void Encrypt()
        {
            Debug.Log("Key bytes: " + string.Join(", ", MakeKeyBytes(pwd)));

            GameObject avatar = DuplicateAvatar(gameObject);

            int progress = 0;

            var mips = new Dictionary<int, Texture2D>();

            byte[] key_bytes = MakeKeyBytes(pwd);
            injector.Init(key_bytes, rounds, filter);

            foreach (var mat in material_list)
            {
                EditorUtility.DisplayProgressBar("Encrypt...", "Encrypt Progress " + ++progress + " of " + material_list.Count, (float)progress / (float)material_list.Count);
                //////////////Condition check///////////////////
                if (!injector.IsSupportShader(mat.shader))
                {
                    Debug.LogError(mat.name + "is unsupported shader!");
                    continue;
                }
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
                //////////////////////////////////////////////
                int size = Math.Max(mat.mainTexture.width, mat.mainTexture.height);
                if (!mips.ContainsKey(size))
                {
                    var mip = encrypt.GenerateRefMipmap(size, size);
                    mips.Add(size, mip);
                }

                Texture2D main_texture = (Texture2D)mat.mainTexture; 
                SetRWEnableTexture(main_texture);

                Texture2D encrypted_tex;
                try
                {
                    encrypted_tex = encrypt.TextureEncrypt(main_texture, key_bytes, rounds);
                    bool xxtea = !encrypt.HasAlpha(encrypted_tex);
                    if (!injector.Inject(mat.shader, dir + "/Decrypt.cginc", encrypted_tex, xxtea))
                        continue;
                }
                catch (UnityException e)
                {
                    Debug.LogError(e.Message);
                    continue;
                }

                if (dir[dir.Length - 1] == '/')
                    dir = dir.Remove(dir.Length - 1);

                if (!AssetDatabase.IsValidFolder(dir + '/' + gameObject.name))
                    AssetDatabase.CreateFolder(dir, gameObject.name);
                if (!AssetDatabase.IsValidFolder(dir + '/' + gameObject.name + "/mat"))
                    AssetDatabase.CreateFolder(dir + '/' + gameObject.name, "mat");

                AssetDatabase.CreateAsset(encrypted_tex, dir + '/' + gameObject.name + '/' + main_texture.name + "_encrypt.asset");
                    
                /////////////////Materials///////////////////////
                Material new_mat = new Material(mat.shader);
                new_mat.CopyPropertiesFromMaterial(mat);
                new_mat.mainTexture = encrypted_tex;
                new_mat.SetTexture("_MipTex", mips[Math.Max(encrypted_tex.width, encrypted_tex.height)]);

                AssetDatabase.CreateAsset(new_mat, dir + '/' + gameObject.name + "/mat/" + mat.name + "_encrypt.mat");
                var renderers = avatar.GetComponentsInChildren<MeshRenderer>();
                for (int i = 0; i < renderers.Length; ++i)
                {
                    var mats = renderers[i].sharedMaterials;
                    for (int j = 0; j < mats.Length; ++j)
                    {
                        if (mats[j].name == mat.name)
                            mats[j] = new_mat;
                    }
                    renderers[i].sharedMaterials = mats;
                }
                var skinned_renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int i = 0; i < skinned_renderers.Length; ++i)
                {
                    var mats = skinned_renderers[i].sharedMaterials;
                    for (int j = 0; j < mats.Length; ++j)
                    {
                        if (mats[j].name == mat.name)
                            mats[j] = new_mat;
                    }
                    skinned_renderers[i].sharedMaterials = mats;
                }
                //////////////////////////////////////////////////
            }
            foreach (var mip in mips)
                AssetDatabase.CreateAsset(mip.Value, dir + '/' + gameObject.name + "/mip_" + mip.Key + ".asset");

            EditorUtility.ClearProgressBar();

            gameObject.SetActive(false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            DestroyImmediate(avatar.GetComponent<ShellProtector>());
        }
    }
}
#endif