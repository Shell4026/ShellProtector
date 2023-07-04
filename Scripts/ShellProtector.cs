using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

#if POIYOMI
using Thry;
#endif

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
        Injector injector;
        ShaderManager shader_manager = ShaderManager.GetInstance();

        public string asset_dir = "Assets/ShellProtect";
        public string pwd = "password";

        [SerializeField]
        int rounds = 32;
        [SerializeField]
        int filter = 1;
        [SerializeField]
        int algorithm = 0;
        
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
            Debug.Log("Test");
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

        bool ConditionCheck(Material mat)
        {
            if (shader_manager.IsPoiyomi(mat.shader))
            {
                if (!shader_manager.IsLockPoiyomi(mat.shader))
                {
#if POIYOMI
                    ShaderOptimizer.SetLockedForAllMaterials(new[] { mat }, 1, true);
#endif
                }
            }
            if (mat.mainTexture.width % 2 != 0 && mat.mainTexture.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", mat.mainTexture.name);
                return false;
            }
            if (injector.WasInjected(mat.shader))
            {
                Debug.LogWarning(mat.name + ": The shader is already encrypted.");
                return false;
            }
            return true;
        }

        public void Encrypt()
        {
            Debug.Log("Key bytes: " + string.Join(", ", MakeKeyBytes(pwd)));

            GameObject avatar = DuplicateAvatar(gameObject);

            var mips = new Dictionary<int, Texture2D>();

            if (asset_dir[asset_dir.Length - 1] == '/')
                asset_dir = asset_dir.Remove(asset_dir.Length - 1);

            byte[] key_bytes = MakeKeyBytes(pwd);

            if (!AssetDatabase.IsValidFolder(asset_dir + '/' + gameObject.name))
                AssetDatabase.CreateFolder(asset_dir, gameObject.name);
            if (!AssetDatabase.IsValidFolder(asset_dir + '/' + gameObject.name + "/mat"))
                AssetDatabase.CreateFolder(asset_dir + '/' + gameObject.name, "mat");
            if (!AssetDatabase.IsValidFolder(asset_dir + '/' + gameObject.name + "/shader"))
                AssetDatabase.CreateFolder(asset_dir + '/' + gameObject.name, "shader");
            int progress = 0;
            foreach (var mat in material_list)
            {
                EditorUtility.DisplayProgressBar("Encrypt...", "Encrypt Progress " + ++progress + " of " + material_list.Count, (float)progress / (float)material_list.Count);
                injector = InjectorFactory.GetInjector(mat.shader);
                if (injector == null)
                {
                    Debug.LogWarning(mat.shader + " is a unsupported shader!");
                    continue;
                }
                if (!ConditionCheck(mat))
                    continue;

                injector.Init(gameObject, key_bytes, filter, asset_dir, rounds);

                int size = Math.Max(mat.mainTexture.width, mat.mainTexture.height);
                if (!mips.ContainsKey(size))
                {
                    var mip = encrypt.GenerateRefMipmap(size, size);
                    mips.Add(size, mip);
                }

                Texture2D main_texture = (Texture2D)mat.mainTexture; 
                SetRWEnableTexture(main_texture);

                Texture2D encrypted_tex = encrypt.TextureEncryptXXTEA(main_texture, key_bytes);
                Shader encrypted_shader;
                try
                {
                    encrypted_shader = injector.Inject(mat, asset_dir + "/Decrypt.cginc", encrypted_tex);
                    if (encrypted_shader == null)
                    {
                        Debug.LogWarning("Injection failed");
                        continue;
                    }
                }
                catch (UnityException e)
                {
                    Debug.LogError(e.Message);
                    continue;
                }

                AssetDatabase.CreateAsset(encrypted_tex, asset_dir + '/' + gameObject.name + '/' + main_texture.name + "_encrypt.asset");
                /////////////////Materials///////////////////////
                Material new_mat = new Material(mat.shader);
                new_mat.CopyPropertiesFromMaterial(mat);
                new_mat.shader = encrypted_shader;
                new_mat.mainTexture = encrypted_tex;
                //Debug.Log(string.Join(", ", new_mat.GetTexturePropertyNames()));
                //Debug.Log(mips[Math.Max(encrypted_tex.width, encrypted_tex.height)]);
                new_mat.SetTexture("_MipTex", mips[Math.Max(encrypted_tex.width, encrypted_tex.height)]);
                
                AssetDatabase.CreateAsset(new_mat, asset_dir + '/' + gameObject.name + "/mat/" + mat.name + "_encrypt.mat");
                
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
                AssetDatabase.CreateAsset(mip.Value, asset_dir + '/' + gameObject.name + "/mip_" + mip.Key + ".asset");

            EditorUtility.ClearProgressBar();

            gameObject.SetActive(false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach(var mat in material_list)
            {
                Texture encrypted_tex = mat.mainTexture;
                mat.SetTexture("_MipTex", mips[Math.Max(encrypted_tex.width, encrypted_tex.height)]);
            }

            DestroyImmediate(avatar.GetComponent<ShellProtector>());
        }
    }
}
#endif