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
            string debug_txt = "";
            foreach (var i in MakeKeyBytes(pwd))
                debug_txt += i.ToString() + ' ';
            Debug.Log("Key bytes: " + debug_txt);

            GameObject avatar = DuplicateAvatar(gameObject);

            int progress = 0;
            foreach (var mat in material_list)
            {
                EditorUtility.DisplayProgressBar("Encrypt...", "Encrypt...Progress " + ++progress + " of " + material_list.Count, (float)progress / (float)material_list.Count);
                if (injector.IsSupportShader(mat.shader))
                {
                    byte[] key_bytes = MakeKeyBytes(pwd);
                    injector.Init(key_bytes, rounds, filter);

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
                    Texture2D main_texture = (Texture2D)mat.mainTexture; 
                    SetRWEnableTexture(main_texture);

                    Texture2D[] tex_set;
                    try
                    {
                        tex_set = encrypt.TextureEncrypt(main_texture, key_bytes, rounds);
                        if (!injector.Inject(mat.shader, dir + "/Decrypt.cginc", tex_set[0]))
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

                    AssetDatabase.CreateAsset(tex_set[0], dir + '/' + gameObject.name + '/' + main_texture.name + "_encrypt.asset");
                    AssetDatabase.CreateAsset(tex_set[1], dir + '/' + gameObject.name + '/' + main_texture.name + "_encrypt_mip.asset");
                    /////////////////Materials///////////////////////
                    Material new_mat = new Material(mat.shader);
                    new_mat.CopyPropertiesFromMaterial(mat);
                    new_mat.mainTexture = tex_set[0];
                    new_mat.SetTexture("_MipTex", tex_set[1]);

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
                else
                {
                    Debug.LogError("Unsupported shader!");
                    continue;
                }
            }
            EditorUtility.ClearProgressBar();

            gameObject.SetActive(false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            DestroyImmediate(avatar.GetComponent<ShellProtector>());
        }
    }
}
#endif