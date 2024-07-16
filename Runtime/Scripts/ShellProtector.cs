#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using System.Security.Cryptography;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Avatars.Components;
using System.Linq;
using VRC.SDKBase;
using UnityEditor.Animations;
using UnityEngine.XR;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.IsisMtt.Ocsp;






#if POIYOMI
using Thry;
#endif

namespace Shell.Protector
{
    public class ShellProtector : MonoBehaviour, IEditorOnly
    {
        [SerializeField]
        List<GameObject> gameobject_list = new List<GameObject>();
        [SerializeField]
        List<Material> material_list = new List<Material>();
        [SerializeField]
        List<Texture2D> texture_list = new List<Texture2D>();
        [SerializeField]
        List<SkinnedMeshRenderer> obfuscationRenderers = new();

        EncryptTexture encrypt = new EncryptTexture();
        Injector injector;
        AssetManager shader_manager = AssetManager.GetInstance();

        public string asset_dir = "Assets/ShellProtect";
        public string pwd = "password"; // fixed password
        public string pwd2 = "pass"; // user password
        public int lang_idx = 0;
        public string lang = "kor";

        bool init = false;

        //Must clear them before start encrypting//
        HashSet<GameObject> meshes = new HashSet<GameObject>();
        HashSet<Texture2D> fallbackTextures = new();
        Dictionary<Material, Material> encryptedMaterials = new Dictionary<Material, Material>();
        //////////////////////////////////

        [SerializeField] uint rounds = 20;
        [SerializeField] int filter = 1;
        [SerializeField] int algorithm = 1;
        [SerializeField] int key_size_idx = 3;
        [SerializeField] int key_size = 12;
        [SerializeField] float animation_speed = 256.0f;
        [SerializeField] bool delete_folders = true;
        [SerializeField] bool parameter_multiplexing = false;
        [SerializeField] bool bUseSmallMipTexture = true;

        [SerializeField] bool bPreserveMMD = true;

        [SerializeField] float fallbackTime = 3.0f;

        public void OnEnable()
        {
            if (init)
                return;

            HashSet<SkinnedMeshRenderer> rednererSet = new();
            Transform child = transform.Find("Body");
            if (child != null)
            {
                SkinnedMeshRenderer renderer = child.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null)
                {
                    Mesh mesh = renderer.sharedMesh;
                    if (mesh != null)
                    {
                        rednererSet.Add(renderer);
                    }
                        
                }
            }
            foreach(var renderer in rednererSet)
            {
                obfuscationRenderers.Add(renderer);
            }
            init = true;
        }

        public byte[] GetKeyBytes()
        {
            return KeyGenerator.MakeKeyBytes(pwd, pwd2, key_size);
        }
        public EncryptTexture GetEncryptTexture()
        {
            return encrypt;
        }
        public void Test2()
        {
            byte[] data_byte = new byte[12] { 255, 250, 245, 240, 235, 230, 225, 220, 215, 210, 205, 200 };
            byte[] key_byte = KeyGenerator.MakeKeyBytes(pwd, pwd2, key_size);

            uint[] data = new uint[3];
            data[0] = (uint)(data_byte[0] | (data_byte[1] << 8) | (data_byte[2] << 16) | (data_byte[3] << 24));
            data[1] = (uint)(data_byte[4] | (data_byte[5] << 8) | (data_byte[6] << 16) | (data_byte[7] << 24));
            data[2] = (uint)(data_byte[8] | (data_byte[9] << 8) | (data_byte[10] << 16) | (data_byte[11] << 24));

            uint[] key = new uint[4];
            key[0] = (uint)(key_byte[0] | (key_byte[1] << 8) | (key_byte[2] << 16) | (key_byte[3] << 24));
            key[1] = (uint)(key_byte[4] | (key_byte[5] << 8) | (key_byte[6] << 16) | (key_byte[7] << 24));
            key[2] = (uint)(key_byte[8] | (key_byte[9] << 8) | (key_byte[10] << 16) | (key_byte[11] << 24));
            key[3] = (uint)(key_byte[12] | (key_byte[13] << 8) | (key_byte[14] << 16) | (key_byte[15] << 24));

            Debug.Log("Key bytes: " + string.Join(", ", key_byte));
            Debug.Log(string.Format("key1:{0}, key2:{1}, key3:{2}", key[0], key[1], key[2]));
            Debug.Log("Data: " + string.Join(", ", data));

            XXTEA xxtea = new XXTEA();
            uint[] result = xxtea.Encrypt(data, key);
            Debug.Log("Encrypted data: " + string.Join(", ", result));

            result = xxtea.Decrypt(result, key);
            Debug.Log("Decrypted data: " + string.Join(", ", result));
        }

        public void Test3()
        {
            byte[] data_byte = new byte[8] { 255, 255, 245, 240, 235, 230, 225, 220 };
            byte[] key_byte = KeyGenerator.MakeKeyBytes(pwd, pwd2, key_size);

            uint[] data = new uint[2];
            data[0] = (uint)(data_byte[0] | (data_byte[1] << 8) | (data_byte[2] << 16) | (data_byte[3] << 24));
            data[1] = (uint)(data_byte[4] | (data_byte[5] << 8) | (data_byte[6] << 16) | (data_byte[7] << 24));

            uint[] key = new uint[4];
            key[0] = (uint)(key_byte[0] | (key_byte[1] << 8) | (key_byte[2] << 16) | (key_byte[3] << 24));
            key[1] = (uint)(key_byte[4] | (key_byte[5] << 8) | (key_byte[6] << 16) | (key_byte[7] << 24));
            key[2] = (uint)(key_byte[8] | (key_byte[9] << 8) | (key_byte[10] << 16) | (key_byte[11] << 24));
            key[3] = (uint)(key_byte[12] | (key_byte[13] << 8) | (key_byte[14] << 16) | (key_byte[15] << 24));

            Debug.Log("Key bytes: " + string.Join(", ", key_byte));
            Debug.Log(string.Format("key1:{0}, key2:{1}, key3:{2}", key[0], key[1], key[2]));
            Debug.Log("Data: " + string.Join(", ", data));

            Chacha20 chacha = new Chacha20();
            uint[] result = chacha.Encrypt(data, key);
            Debug.Log("Encrypted data: " + string.Join(", ", result));
            result = chacha.Encrypt(result, key);
            Debug.Log("Decrypted data: " + string.Join(", ", result));
        }
        public static void SetRWEnableTexture(Texture2D texture)
        {
            if (texture.isReadable)
                return;
            string path = AssetDatabase.GetAssetPath(texture);
            string meta = File.ReadAllText(path + ".meta");

            meta = Regex.Replace(meta, "isReadable: 0", "isReadable: 1");
            File.WriteAllText(path + ".meta", meta);

            AssetDatabase.Refresh();
        }
        public static void SetCrunchCompression(Texture2D texture, bool crunch)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            string meta = File.ReadAllText(path + ".meta");

            if (crunch == false)
            {
                if (texture.format == TextureFormat.DXT1Crunched)
                {
                    int format = 10;
                    meta = Regex.Replace(meta, "textureFormat: \\d+", "textureFormat: " + format);
                }
                else if (texture.format == TextureFormat.DXT5Crunched)
                {
                    int format = 12;
                    meta = Regex.Replace(meta, "textureFormat: \\d+", "textureFormat: " + format);
                }
            }
            int enable = crunch ? 1 : 0;
            meta = Regex.Replace(meta, "crunchedCompression: \\d+", "crunchedCompression: " + enable);
            File.WriteAllText(path + ".meta", meta);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        public static void SetGenerateMipmap(Texture2D texture, bool generate)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            string meta = File.ReadAllText(path + ".meta");

            int enable = generate ? 1 : 0;
            meta = Regex.Replace(meta, "enableMipMap: \\d+", "enableMipMap: " + enable);
            File.WriteAllText(path + ".meta", meta);

            AssetDatabase.Refresh();
        }

        public GameObject DuplicateAvatar(GameObject avatar)
        {
            GameObject cpy = Instantiate(avatar);
            if(!avatar.name.Contains("_encrypted"))
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
            if (mat.mainTexture == null)
            {
                Debug.LogWarningFormat("{0} : The mainTexture is empty. it will be skip.", mat.name);
                return false;
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
            var av3 = gameObject.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (av3 == null)
            {
                Debug.LogError(gameObject.name + ": can't find VRCAvatarDescriptor!");
                return false;
            }
            if(av3.expressionParameters == null)
            {
                Debug.LogError(gameObject.name + ": can't find expressionParmeters!");
                return false;
            }
            return true;
        }

        public void CreateFolders()
        {
            if (!AssetDatabase.IsValidFolder(asset_dir + '/' + gameObject.name))
                AssetDatabase.CreateFolder(asset_dir, gameObject.name);
            else
            {
                if (delete_folders)
                {
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, gameObject.name, "animations"));
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, gameObject.name, "mat"));
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, gameObject.name, "shader"));
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, gameObject.name, "tex"));
                }
            }
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, gameObject.name, "tex")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, gameObject.name), "tex");
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, gameObject.name, "mat")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, gameObject.name), "mat");
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, gameObject.name, "shader")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, gameObject.name), "shader");
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, gameObject.name, "animations")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, gameObject.name), "animations");
        }

        public List<Material> GetMaterials()
        {
            List<Material> materials = new List<Material>();
            foreach (GameObject g in gameobject_list)
            {
                if (g == null)
                    continue;

                var meshRenderers = g.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var meshRenderer in meshRenderers)
                {
                    foreach (var material in meshRenderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            materials.Add(material);
                        }
                    }
                }

                var skinnedMeshRenderers = g.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                {
                    foreach (var material in skinnedMeshRenderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            materials.Add(material);
                        }
                    }
                }
            }

            return materials.Concat(material_list).Distinct().ToList();
        }

        public GameObject Encrypt(bool isModular = true)
        {
            return Encrypt(bUseSmallMipTexture, isModular);
        }

        public GameObject Encrypt(bool bUseSmallMip, bool isModular = true)
        {
            meshes.Clear();
            encryptedMaterials.Clear();
            fallbackTextures.Clear();

            gameObject.SetActive(true);
            Debug.Log("Key bytes: " + string.Join(", ", GetKeyBytes()));

            var materials = GetMaterials();

            GameObject avatar;
            if (!isModular)
            {
                avatar = DuplicateAvatar(gameObject);
                Debug.Log("Duplicate avatar success.");
            }
            else
            {
                avatar = gameObject;
            }

            if (avatar == null)
            {
                Debug.LogError("Cannot create duplicated avatar!");
                return null;
            }

            var mips = new Dictionary<int, Texture2D>();

            byte[] key_bytes = GetKeyBytes();

            CreateFolders();

            ///////////////////Select crypto algorithm/////////////////////
            IEncryptor encryptor = new XXTEA();
            if (algorithm == 0)
            {
                XXTEA xxtea = new XXTEA();
                xxtea.m_rounds = rounds;
                encryptor = xxtea;
            }
            else if(algorithm == 1) 
            {
                Chacha20 chacha = new Chacha20();
                byte[] hash1 = KeyGenerator.GetKeyHash(key_bytes, KeyGenerator.GenerateRandomString(chacha.nonce.Length));
                Array.Copy(hash1, 0, chacha.nonce, 0, chacha.nonce.Length);
                encryptor = chacha;
            }
            ///////////////////////////////////////////////////////////////
            int progress = 0;
            foreach (var mat in materials)
            {
                if (mat == null)
                {
                    ++progress;
                    Debug.LogErrorFormat("mat is null!");
                    continue;
                }
                EditorUtility.DisplayProgressBar("Encrypt...", "Encrypt Progress " + ++progress + " of " + material_list.Count, (float)progress / (float)materials.Count);
                injector = InjectorFactory.GetInjector(mat.shader);
                if (injector == null)
                {
                    Debug.LogWarning(mat.shader + " is a unsupported shader! supported type:lilToon, poiyomi");
                    continue;
                }
                if (!ConditionCheck(mat))
                    continue;

                Debug.LogFormat("{0} : start encrypt...", mat.name);

                Texture2D main_texture = (Texture2D)mat.mainTexture;
                injector.Init(gameObject, main_texture, key_bytes, key_size, filter, asset_dir, encryptor);

                #region Generate mip_tex
                int size = Math.Max(mat.mainTexture.width, mat.mainTexture.height);
                if (!mips.ContainsKey(size))
                {
                    var mip = encrypt.GenerateRefMipmap(size, size, bUseSmallMip);
                    if (mip == null)
                        Debug.LogErrorFormat("{0} : Can't generate mip tex{1}.", mat.name, size);
                    else
                    {
                        mips.Add(size, mip);
                        AssetDatabase.CreateAsset(mip, Path.Combine(asset_dir, gameObject.name, "tex", "mip_" + size + ".asset"));
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }
                #endregion
                SetRWEnableTexture(main_texture);
                SetCrunchCompression(main_texture, false);
                SetGenerateMipmap(main_texture, true);

                Texture2D lim_texture = null;
                Texture2D lim_texture2 = null;
                Texture2D outline_texture = null;
                Texture2D limShadeTexture = null;

                bool has_lim_texture = false;
                bool has_lim_texture2 = false;
                bool has_outline_texture = false;
                bool hasLimShadeTexture = false;
                #region Get lim, outline tex
                if (shader_manager.IsPoiyomi(mat.shader))
                {
                    var tex_properties = mat.GetTexturePropertyNames();
                    foreach(var t in tex_properties)
                    {
                        if(t == "_RimTex")
                            lim_texture = (Texture2D)mat.GetTexture(t);
                        else if(t == "_Rim2Tex")
                            lim_texture2 = (Texture2D)mat.GetTexture(t);
                        else if(t == "_OutlineTexture")
                            outline_texture = (Texture2D)mat.GetTexture(t);
                    }
                }
                else if (shader_manager.IslilToon(mat.shader))
                {
                    var tex_properties = mat.GetTexturePropertyNames();
                    foreach (var t in tex_properties)
                    {
                        if (t == "_RimColorTex")
                            lim_texture = (Texture2D)mat.GetTexture(t);
                        else if (t == "_OutlineTex")
                            outline_texture = (Texture2D)mat.GetTexture(t);
                        else if (t == "_RimShadeMask")
                            limShadeTexture = (Texture2D)mat.GetTexture(t);
                    }
                }
                if (lim_texture != null)
                {
                    if (main_texture.GetInstanceID() == lim_texture.GetInstanceID())
                        has_lim_texture = true;
                }
                if (lim_texture2 != null)
                {
                    if (main_texture.GetInstanceID() == lim_texture2.GetInstanceID())
                        has_lim_texture2 = true;
                }
                if (outline_texture != null)
                {
                    if (main_texture.GetInstanceID() == outline_texture.GetInstanceID())
                        has_outline_texture = true;
                }
                if (limShadeTexture != null)
                {
                    if (main_texture.GetInstanceID() == limShadeTexture.GetInstanceID())
                        hasLimShadeTexture = true;
                }
                #endregion
                string encrypt_tex_path = Path.Combine(asset_dir, gameObject.name, "tex", main_texture.name + "_encrypt.asset");
                string encrypt_tex2_path = Path.Combine(asset_dir, gameObject.name, "tex", main_texture.name + "_encrypt2.asset");
                string encrypted_mat_path = Path.Combine(asset_dir, gameObject.name, "mat", mat.name + "_encrypted.mat");
                string encrypted_shader_path = Path.Combine(asset_dir, gameObject.name, "shader", mat.name);
                Texture2D[] encrypted_tex = new Texture2D[2] { null, null };

                #region Textures Duplicate Check
                bool has_exist_encrypt_tex = false;
                bool has_exist_encrypt_tex2 = false;
                foreach (var mat_tmp in materials)
                {
                    try
                {
                    if (mat_tmp == mat)
                        continue;

                    Texture2D main_tex = mat_tmp.mainTexture as Texture2D;
                    if (main_tex == null)
                        continue;

                    if(main_texture.GetInstanceID() == main_tex.GetInstanceID())
                    {
                        encrypted_tex[0] = AssetDatabase.LoadAssetAtPath(encrypt_tex_path, typeof(Texture2D)) as Texture2D;
                        encrypted_tex[1] = AssetDatabase.LoadAssetAtPath(encrypt_tex2_path, typeof(Texture2D)) as Texture2D;
                        if (encrypted_tex[0] != null)
                            has_exist_encrypt_tex = true;
                        if (encrypted_tex[1] != null)
                            has_exist_encrypt_tex2 = true;
                        break;
                    }
                    else
                    {
                        if(main_texture.name == main_tex.name)
                        {
                            encrypted_tex[0] = AssetDatabase.LoadAssetAtPath(encrypt_tex_path, typeof(Texture2D)) as Texture2D;
                            int idx = 0;
                            while (encrypted_tex[0] != null)
                            {
                                encrypt_tex_path = Path.Combine(asset_dir, gameObject.name, "tex", main_texture.name + idx + "_encrypt.asset");
                                encrypt_tex2_path = Path.Combine(asset_dir, gameObject.name, "tex", main_texture.name + idx + "_encrypt2.asset");

                                encrypted_tex[0] = AssetDatabase.LoadAssetAtPath(encrypt_tex_path, typeof(Texture2D)) as Texture2D;
                                ++idx;
                            }
                        }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                        continue;
                    }
                }
                #endregion

                #region Materials Duplicate Check
                for (int i = 0; i < materials.Count; ++i)
                {
                    if (materials[i] == null)
                        continue;
                    if (mat.GetInstanceID() == materials[i].GetInstanceID())
                        continue;
                    else
                    {
                        if (mat.name == materials[i].name)
                        {
                            Material m = AssetDatabase.LoadAssetAtPath(encrypted_mat_path, typeof(Material)) as Material;
                            int idx = 0;
                            while(m != null)
                            {
                                encrypted_mat_path = Path.Combine(asset_dir, gameObject.name, "mat", mat.name + idx + "_encrypted.mat");
                                encrypted_shader_path = Path.Combine(asset_dir, gameObject.name, "shader", mat.name + idx);
                                m = AssetDatabase.LoadAssetAtPath(encrypted_mat_path, typeof(Material)) as Material;
                                ++idx;
                            }
                        }
                    }    
                }
                #endregion

                #region Make encrypted textures
                if (algorithm == 1)
                {
                    Chacha20 chacha = encryptor as Chacha20;
                    byte[] hashMat = KeyGenerator.GetHash(mat.GetInstanceID());
                    for (int i = 0; i < chacha.nonce.Length; ++i)
                        chacha.nonce[i] ^= hashMat[i];
                }

                if (has_exist_encrypt_tex == false)
                {
                    encrypted_tex = encrypt.TextureEncrypt(main_texture, key_bytes, encryptor);
                    if (encrypted_tex == null)
                    {
                        Debug.LogErrorFormat("{0} : encrypt failed0.", main_texture.name);
                        continue;
                    }
                    if (encrypted_tex[0] == null)
                    {
                        Debug.LogErrorFormat("{0} : encrypt failed1.", main_texture.name);
                        continue;
                    }
                    AssetDatabase.CreateAsset(encrypted_tex[0], encrypt_tex_path);
                    Debug.Log(encrypted_tex[0].name + ": " + AssetDatabase.GetAssetPath(encrypted_tex[0]));
                }
                if (has_exist_encrypt_tex2 == false)
                {
                    if (encrypted_tex[1] != null)
                        AssetDatabase.CreateAsset(encrypted_tex[1], encrypt_tex2_path);
                }
                #endregion

                Shader encrypted_shader;
                try
                {
                    encrypted_shader = injector.Inject(mat, Path.Combine(asset_dir, "Decrypt.cginc"), encrypted_shader_path, encrypted_tex[0], has_lim_texture, has_lim_texture2, has_outline_texture);
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

                string fallbackDir = Path.Combine(asset_dir, gameObject.name, "tex", main_texture.name + "_fallback.asset");
                Texture2D fallback = null;
                if (!fallbackTextures.Contains(main_texture))
                {
                    fallback = encrypt.GenerateFallback(main_texture);
                    if (fallback != null)
                    {
                        fallbackTextures.Add(main_texture);
                        AssetDatabase.CreateAsset(fallback, fallbackDir);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }
                else
                {
                    fallback = AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackDir);
                }

                #region Material
                Material new_mat = new Material(mat.shader);
                new_mat.CopyPropertiesFromMaterial(mat);
                new_mat.shader = encrypted_shader;
                var original_tex = new_mat.mainTexture;
                new_mat.mainTexture = fallback;

                int max = Math.Max(encrypted_tex[0].width, encrypted_tex[0].height);
                var mip_tex = mips[max];
                if(mip_tex == null)
                    Debug.LogWarningFormat("mip_{0} is not exsist", max);

                new_mat.SetTexture("_MipTex", mip_tex);

                if (encrypted_tex[0] != null)
                    new_mat.SetTexture("_EncryptTex0", encrypted_tex[0]);
                if (encrypted_tex[1] != null)
                    new_mat.SetTexture("_EncryptTex1", encrypted_tex[1]);

                if(has_lim_texture)
                {
                    if (shader_manager.IsPoiyomi(mat.shader))
                        new_mat.SetTexture("_RimTex", encrypted_tex[0]);
                    else if (shader_manager.IslilToon(mat.shader))
                        new_mat.SetTexture("_RimColorTex", encrypted_tex[0]);
                }
                if(has_lim_texture2)
                {
                    if (shader_manager.IsPoiyomi(mat.shader))
                        new_mat.SetTexture("_Rim2Tex", encrypted_tex[0]);
                }
                if(has_outline_texture)
                {
                    if (shader_manager.IsPoiyomi(mat.shader))
                        new_mat.SetTexture("_OutlineTexture", fallback);
                    else if(shader_manager.IslilToon(mat.shader))
                        new_mat.SetTexture("_OutlineTex", fallback);
                }
                if(hasLimShadeTexture) //only liltoon
                {
                    if (shader_manager.IslilToon(mat.shader))
                        new_mat.SetTexture("_RimShadeMask", fallback);
                }

                new_mat.renderQueue = mat.renderQueue;
                #endregion

                #region Remove Duplicate Textures
                foreach (var name in new_mat.GetTexturePropertyNames()) 
                {
                    if (new_mat.GetTexture(name) == null)
                        continue;
                    if (new_mat.GetTexture(name).GetInstanceID() == original_tex.GetInstanceID())
                        new_mat.SetTexture(name, null);
                }
                #endregion

                AssetDatabase.CreateAsset(new_mat, encrypted_mat_path);
                Debug.LogFormat("{0} : create encrypted material : {1}", mat.name, AssetDatabase.GetAssetPath(new_mat));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var renderers = avatar.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Length; ++i)
                    {
                        var mats = renderers[i].sharedMaterials;
                        if (mats == null)
                            continue;
                        for (int j = 0; j < mats.Length; ++j)
                        {
                            if (mats[j] == null)
                                continue;
                            if (mats[j].GetInstanceID() == mat.GetInstanceID())
                            {
                                mats[j] = new_mat;
                                meshes.Add(renderers[i].gameObject);
                            }
                        }
                        renderers[i].sharedMaterials = mats;
                    }
                }
                var skinned_renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skinned_renderers != null)
                {
                    for (int i = 0; i < skinned_renderers.Length; ++i)
                    {
                        var mats = skinned_renderers[i].sharedMaterials;
                        if (mats == null)
                            continue;
                        for (int j = 0; j < mats.Length; ++j)
                        {
                            if (mats[j] == null)
                                continue;
                            if (mats[j].GetInstanceID() == mat.GetInstanceID())
                            {
                                mats[j] = new_mat;
                                meshes.Add(skinned_renderers[i].gameObject);
                            }
                        }
                        skinned_renderers[i].sharedMaterials = mats;
                    }
                }
                //////////////////////////////////////////////////
                if(!encryptedMaterials.ContainsKey(mat))
                    encryptedMaterials.Add(mat, new_mat);
            }
            EditorUtility.ClearProgressBar();

            ///////////////////////parameter////////////////////
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            av3.expressionParameters = ParameterManager.AddKeyParameter(av3.expressionParameters, key_size, parameter_multiplexing);
            AssetDatabase.CreateAsset(av3.expressionParameters, asset_dir + "/" + gameObject.name + "/" + av3.expressionParameters.name + ".asset");
            ////////////////////////////////////////////////////
            SetMaterialFallbackValue(avatar, true);
            if (!isModular)
            {
                gameObject.SetActive(false);

                var tester = avatar.AddComponent<ShellProtectorTester>();
                tester.lang = lang;
                tester.lang_idx = lang_idx;
                tester.protector = this;
                tester.user_key_length = key_size;
                Selection.activeObject = tester;

                SetAnimations(avatar, true);
                ObfuscateBlendShape(avatar, true);
                ChangeMaterialsInAnims(avatar, true);
                CleanComponent(avatar);    
            }

            return avatar;
        }

        public void SetAnimations(GameObject avatar, bool clone)
        {
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            AnimatorController fx;
            if (clone)
                fx = AnimatorManager.DuplicateAnimator(av3.baseAnimationLayers[4].animatorController, Path.Combine(asset_dir, gameObject.name));
            else
                fx = av3.baseAnimationLayers[4].animatorController as AnimatorController;

            av3.baseAnimationLayers[4].animatorController = fx;
            string animation_dir = Path.Combine(asset_dir, gameObject.name, "animations");

            GameObject[] mesh_array = new GameObject[meshes.Count];
            meshes.CopyTo(mesh_array);
            AnimatorManager.CreateKeyAniamtions(Path.Combine(asset_dir, "Animations"), animation_dir, mesh_array);
            var fallbackAnim = AnimatorManager.CreateFallbackAniamtions(Path.Combine(asset_dir, "Animations", "FallbackOff.anim"), animation_dir, mesh_array);
            AnimatorManager.AddKeyLayer(fx, animation_dir, key_size, animation_speed, parameter_multiplexing);
            AnimatorManager.AddFallbackLayer(fx, fallbackAnim, fallbackTime);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public void CleanComponent(GameObject avatar)
        {
            DestroyImmediate(avatar.GetComponent<ShellProtector>());
        }

        public void ChangeMaterialsInAnims(GameObject avatar, bool clone)
        {
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            var fx = av3.baseAnimationLayers[4].animatorController as AnimatorController;
            string animationDir = Path.Combine(asset_dir, gameObject.name, "animations");

            AnimatorManager animManager = new();
            foreach (var pair in encryptedMaterials)
            {
                animManager.ChangeAnimationMaterial(fx, pair.Key, pair.Value, clone, animationDir);
            }
        }

        public VRCExpressionParameters GetParameter()
        {
            var av3 = gameObject.GetComponent<VRCAvatarDescriptor>();
            if (av3 == null)
                return null;
            return av3.expressionParameters;
        }

        public static AnimatorController Getfx(GameObject avatar)
        {
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (av3 == null)
                return null;
            return av3.baseAnimationLayers[4].animatorController as AnimatorController;
        }

        public void ObfuscateBlendShape(GameObject avatar, bool clone)
        {
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            AnimatorController fx = Getfx(avatar);
            string animDir = Path.Combine(asset_dir, gameObject.name, "animations");

            Obfuscator obfuscator = new Obfuscator();
            obfuscator.clone = clone;

            var childRenderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in obfuscationRenderers)
            {
                SkinnedMeshRenderer selectRenderer = null;
                foreach (var childRenderer in childRenderers)
                {
                    if(childRenderer.sharedMesh == renderer.sharedMesh)
                    {
                        selectRenderer = childRenderer;
                        break;
                    }
                }
                if (selectRenderer == null)
                    continue;

                Mesh mesh = selectRenderer.sharedMesh;
                if (mesh == null)
                {
                    Debug.LogErrorFormat("{0} haven't mesh", renderer.transform.name);
                    continue;
                }
                Mesh newMesh = obfuscator.ObfuscateBlendShapeMesh(mesh, Path.Combine(asset_dir, gameObject.name));
                selectRenderer.sharedMesh = newMesh;

                ////////Change renderer component shape keys////////
                List<float> weights = new();
                for (int i = 0; i < newMesh.blendShapeCount; ++i)
                {
                    weights.Add(selectRenderer.GetBlendShapeWeight(i));
                    selectRenderer.SetBlendShapeWeight(i, 0.0f);
                }
                var obList = obfuscator.GetObfuscatedBlendShapeIndex();
                Debug.LogFormat("size: {0}, {1}", newMesh.blendShapeCount, obList.Count);
                for (int i = 0; i < newMesh.blendShapeCount; ++i)
                {
                    selectRenderer.SetBlendShapeWeight(i, weights[obList[i]]);
                }
                /////////////////////////////////
                obfuscator.ObfuscateBlendshapeInAnim(fx, animDir);
                obfuscator.ChangeObfuscatedBlendShapeInDescriptor(av3);
                obfuscator.Clean();
            }
        }

        public static void SetMaterialFallbackValue(GameObject avatar, bool fallback)
        {
            var renderers = avatar.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    var mats = r.sharedMaterials;
                    if (mats == null)
                    {
                        Debug.LogWarning(r.gameObject.name + ": can't find sharedMaterials");
                        continue;
                    }
                    foreach (var mat in mats)
                    {
                        if (mat == null)
                            continue;
                        if (mat.name.Contains("_encrypted"))
                        {
                            mat.SetFloat("_fallback", fallback == true ? 1.0f : 0.0f);
                        }
                    }
                }
            }
            var skinned_renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned_renderers != null)
            {
                foreach (var r in skinned_renderers)
                {
                    var mats = r.sharedMaterials;
                    if (mats == null)
                    {
                        Debug.LogWarning(r.gameObject.name + ": can't find sharedMaterials");
                        continue;
                    }
                    foreach (var mat in mats)
                    {
                        if (mat == null)
                            continue;
                        if (mat.name.Contains("_encrypted"))
                        {
                            mat.SetFloat("_fallback", fallback == true ? 1.0f : 0.0f);
                        }
                    }
                }
            }
        }
    }
}
#endif