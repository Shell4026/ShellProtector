#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Avatars.Components;
using System.Linq;
using VRC.SDKBase;
using UnityEditor.Animations;

#if MODULAR
using nadena.dev.modular_avatar.core;

#endif

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
        List<SkinnedMeshRenderer> obfuscationRenderers = new List<SkinnedMeshRenderer>();

        EncryptTexture encrypt = new EncryptTexture();
        Injector injector;
        AssetManager shader_manager = AssetManager.GetInstance();
        bool init = false;

        public string asset_dir = "Assets/ShellProtect";
        public string pwd = "password"; // fixed password
        public string pwd2 = "pass"; // user password
        public int lang_idx = 0;
        public string lang = "kor";
        public VRCAvatarDescriptor descriptor;
        public class MatOption
        {
            public bool active = true;
            public int filter = -1;
        }
        public Dictionary<Material, MatOption> matOptions = new Dictionary<Material, MatOption>();

        struct ProcessedTexture
        {
            public Texture2D encrypted0;
            public Texture2D encrypted1;
            public Texture2D fallback;
            public byte[] nonce;
        }

        //Must clear them before start encrypting//
        HashSet<GameObject> meshes = new HashSet<GameObject>();
        Dictionary<Material, Material> encryptedMaterials = new Dictionary<Material, Material>();
        Dictionary<Texture2D, ProcessedTexture> processedTextures = new Dictionary<Texture2D, ProcessedTexture>();
        //////////////////////////////////

        [SerializeField] uint rounds = 20;
        [SerializeField] int filter = 1;
        [SerializeField] int algorithm = 1;
        [SerializeField] int key_size_idx = 3;
        [SerializeField] int key_size = 12;
        [SerializeField] float animation_speed = 128.0f;
        [SerializeField] bool delete_folders = true;
        [SerializeField] bool parameter_multiplexing = false;
        [SerializeField] bool bUseSmallMipTexture = true;

        [SerializeField] bool bPreserveMMD = true;

        [SerializeField] float fallbackTime = 5.0f;
        [SerializeField] bool turnOnAllSafetyFallback = true;

        public static readonly string[] filterStrings = new string[2] { "Point", "Bilinear" };

        public void Init()
        {
            if (init)
                return;

            HashSet<SkinnedMeshRenderer> rednererSet = new HashSet<SkinnedMeshRenderer>();
            Transform child = descriptor.transform.Find("Body");
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
            foreach (var renderer in rednererSet)
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
            var av3 = descriptor.gameObject.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (av3 == null)
            {
                Debug.LogError(descriptor.gameObject.name + ": can't find VRCAvatarDescriptor!");
                return false;
            }
            if(av3.expressionParameters == null)
            {
                Debug.LogError(descriptor.gameObject.name + ": can't find expressionParmeters!");
                return false;
            }
            return true;
        }

        public void CreateFolders()
        {
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString())))
            {
                AssetDatabase.CreateFolder(asset_dir, descriptor.gameObject.GetInstanceID().ToString());
            }
            else
            {
                if (delete_folders)
                {
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "animations"));
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "mat"));
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "shader"));
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "tex"));
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "tex")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString()), "tex");
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "mat")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString()), "mat");
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "shader")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString()), "shader");
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "animations")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString()), "animations");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
            processedTextures.Clear();

            MonoScript monoScript = MonoScript.FromMonoBehaviour(this);
            string script_path = AssetDatabase.GetAssetPath(monoScript);
            asset_dir = Path.GetDirectoryName(Path.GetDirectoryName(script_path));
            string avatarDir = Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString());

            Debug.Log("AssetDir: " + asset_dir);

            if (descriptor == null)
            {
                Debug.LogError("Can't find avatar descriptor!");
                return null;
            }

            descriptor.gameObject.SetActive(true);
            Debug.Log("Key bytes: " + string.Join(", ", GetKeyBytes()));

            var materials = GetMaterials();

            GameObject avatar;
            if (!isModular)
            {
                avatar = DuplicateAvatar(descriptor.gameObject);
                Debug.Log("Duplicate avatar success.");
            }
            else
            {
                avatar = descriptor.gameObject;
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
                    continue;
                }
                EditorUtility.DisplayProgressBar("Encrypt...", "Encrypt Progress " + ++progress + " of " + materials.Count, (float)progress / (float)materials.Count);
                injector = InjectorFactory.GetInjector(mat.shader);
                if (injector == null)
                {
                    Debug.LogWarning(mat.shader + " is a unsupported shader! supported type:lilToon, poiyomi");
                    continue;
                }
                if (!ConditionCheck(mat))
                    continue;

                Debug.LogFormat("{0} : Start encrypt...", mat.name);

                int filter = this.filter;
#if UNITY_2022
                MatOption option = matOptions.GetValueOrDefault(mat, null);
#else
                MatOption option = null;
                if (matOptions.ContainsKey(mat))
                    option = matOptions[mat];
#endif
                if (option != null)
                {
                    if (option.active == false)
                    {
                        Debug.LogFormat("{0} : Skip", mat.name);
                        continue;
                    }
                    filter = option.filter;
                }

                Texture2D main_texture = (Texture2D)mat.mainTexture;
                injector.Init(descriptor.gameObject, main_texture, key_bytes, key_size, filter, asset_dir, encryptor);

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
                        AssetDatabase.CreateAsset(mip, Path.Combine(avatarDir, "tex", "mip_" + size + ".asset"));
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }
                #endregion
                SetRWEnableTexture(main_texture);
                SetCrunchCompression(main_texture, false);
                SetGenerateMipmap(main_texture, true);

                Texture2D limTexture = null;
                Texture2D limTexture2 = null;
                Texture2D outlineTexture = null;
                Texture2D limShadeTexture = null;

                #region Get lim, outline tex
                ///////////////////Get lim, outline texture/////////////////
                if (shader_manager.IsPoiyomi(mat.shader))
                {
                    var tex_properties = mat.GetTexturePropertyNames();
                    foreach(var t in tex_properties)
                    {
                        if(t == "_RimTex")
                            limTexture = (Texture2D)mat.GetTexture(t);
                        else if(t == "_Rim2Tex")
                            limTexture2 = (Texture2D)mat.GetTexture(t);
                        else if(t == "_OutlineTexture")
                            outlineTexture = (Texture2D)mat.GetTexture(t);
                    }
                }
                else if (shader_manager.IslilToon(mat.shader))
                {
                    var tex_properties = mat.GetTexturePropertyNames();
                    foreach (var t in tex_properties)
                    {
                        if (t == "_RimColorTex")
                            limTexture = (Texture2D)mat.GetTexture(t);
                        else if (t == "_OutlineTex")
                            outlineTexture = (Texture2D)mat.GetTexture(t);
                        else if (t == "_RimShadeMask")
                            limShadeTexture = (Texture2D)mat.GetTexture(t);
                    }
                }
                ////////////////////////////////////////////////////////////////
                #endregion
                string encrypt_tex_path = Path.Combine(avatarDir, "tex", main_texture.GetInstanceID() + "_encrypt.asset");
                string encrypt_tex2_path = Path.Combine(avatarDir, "tex", main_texture.GetInstanceID() + "_encrypt2.asset");
                string encrypted_mat_path = Path.Combine(avatarDir, "mat", mat.GetInstanceID() + "_encrypted.mat");
                string encrypted_shader_path = Path.Combine(avatarDir, "shader", mat.GetInstanceID().ToString());

                #region Make encrypted textures
                Texture2D[] encrypted_tex = new Texture2D[2] { null, null };
                bool processed = processedTextures.ContainsKey(main_texture);
                ProcessedTexture processedTexture;
                if (processed)
                    processedTexture = processedTextures[main_texture];
                else
                    processedTexture = new ProcessedTexture
                    {
                        encrypted0 = null,
                        encrypted1 = null,
                        fallback = null,
                        nonce = new byte[12]
                    };

                //Set chacha nonce
                if (algorithm == 1)
                {
                    Chacha20 chacha = encryptor as Chacha20;
                    if (!processed)
                    {
                        byte[] hashMat = KeyGenerator.GetHash(mat.GetInstanceID());
                        for (int i = 0; i < chacha.nonce.Length; ++i)
                            chacha.nonce[i] ^= hashMat[i];
                        Array.Copy(chacha.nonce, 0, processedTexture.nonce, 0, processedTexture.nonce.Length);
                    }
                    else
                    {
                        byte[] nonce = processedTextures[main_texture].nonce;
                        Array.Copy(nonce, 0, chacha.nonce, 0, chacha.nonce.Length);
                    }
                }

                if (processed == false)
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
                    processedTexture.encrypted0 = encrypted_tex[0];

                    if (encrypted_tex[1] != null)
                    {
                        AssetDatabase.CreateAsset(encrypted_tex[1], encrypt_tex2_path);
                        processedTexture.encrypted1 = encrypted_tex[1];
                    }
                }
                else
                {
                    encrypted_tex[0] = processedTexture.encrypted0;
                    encrypted_tex[1] = processedTexture.encrypted1;
                }
                #endregion

                Shader encrypted_shader;
                try
                {
                    encrypted_shader = injector.Inject(mat, Path.Combine(asset_dir, "Decrypt.cginc"), encrypted_shader_path, encrypted_tex[0], limTexture != null, limTexture2 != null, outlineTexture != null);
                    Selection.activeObject = encrypted_shader;
                    EditorApplication.ExecuteMenuItem("Assets/Reimport");
                    if (encrypted_shader == null)
                    {
                        Debug.LogErrorFormat("{0}: Injection failed", mat.name);
                        continue;
                    }
                }
                catch (UnityException e)
                {
                    Debug.LogError(e.Message);
                    continue;
                }

                #region FallbackTexture
                /////////////////Generate fallback/////////////////////
                string fallbackDir = Path.Combine(avatarDir, "tex", main_texture.GetInstanceID() + "_fallback.asset");
                Texture2D fallback = processedTexture.fallback;
                if (fallback == null)
                {
                    fallback = encrypt.GenerateFallback(main_texture);
                    if (fallback != null)
                    {
                        processedTexture.fallback = fallback;
                        AssetDatabase.CreateAsset(fallback, fallbackDir);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }
                ////////////////////////////////////////////////////////
                #endregion

                #region Material
                //////////////////Create Material////////////////////////
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

                new_mat.renderQueue = mat.renderQueue;
                if(turnOnAllSafetyFallback)
                {
                    new_mat.SetOverrideTag("VRCFallback", "Unlit");
                }

                #region Remove Duplicate Textures
                ////////////////////Remove Duplicate Textures///////////////////////////////
                if (limTexture != null)
                {
                    string texName = "";
                    if (shader_manager.IsPoiyomi(mat.shader))
                        texName = "_RimTex";
                    else if (shader_manager.IslilToon(mat.shader))
                        texName = "_RimColorTex";

                    if (original_tex == limTexture)
                        new_mat.SetTexture(texName, encrypted_tex[0]);
                    else if(processedTextures.ContainsKey(limTexture))
                        new_mat.SetTexture(texName, null);

                }
                if (limTexture2 != null) //only poiyomi
                {
                    string texName = "";
                    if (shader_manager.IsPoiyomi(mat.shader))
                        texName = "_Rim2Tex";

                    if (original_tex == limTexture2)
                        new_mat.SetTexture(texName, encrypted_tex[0]);
                    else if(processedTextures.ContainsKey(limTexture2))
                        new_mat.SetTexture(texName, null);
                }
                if (outlineTexture != null)
                {
                    string texName = "";
                    if (shader_manager.IsPoiyomi(mat.shader))
                        texName = "_OutlineTexture";
                    else if (shader_manager.IslilToon(mat.shader))
                        texName = "_OutlineTex";

                    if (original_tex == outlineTexture)
                        new_mat.SetTexture(texName, fallback);
                    else if (processedTextures.ContainsKey(outlineTexture))
                        new_mat.SetTexture(texName, processedTextures[outlineTexture].fallback);
                }
                if (limShadeTexture != null) //only liltoon
                {
                    string texName = "_RimShadeMask";
                    if (original_tex == limShadeTexture)
                        new_mat.SetTexture(texName, fallback);
                    else if (processedTextures.ContainsKey(limShadeTexture))
                        new_mat.SetTexture(texName, null);
                }
                foreach (var name in new_mat.GetTexturePropertyNames()) 
                {
                    if (new_mat.GetTexture(name) == null)
                        continue;
                    if (new_mat.GetTexture(name).GetInstanceID() == original_tex.GetInstanceID())
                        new_mat.SetTexture(name, null);
                }
                //////////////////////////////////////////////////////////////////
                #endregion

                AssetDatabase.CreateAsset(new_mat, encrypted_mat_path);
                Debug.LogFormat("{0} : create encrypted material : {1}", mat.name, AssetDatabase.GetAssetPath(new_mat));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                //////////////////////////////////////////////////////
                #endregion

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
                if (!processed)
                    processedTextures.Add(main_texture, processedTexture);
                if (!encryptedMaterials.ContainsKey(mat))
                    encryptedMaterials.Add(mat, new_mat);
            }
            EditorUtility.ClearProgressBar();

            ///////////////////////parameter////////////////////
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            av3.expressionParameters = ParameterManager.AddKeyParameter(av3.expressionParameters, key_size, parameter_multiplexing);
            AssetDatabase.CreateAsset(av3.expressionParameters, Path.Combine(avatarDir, av3.expressionParameters.name + ".asset"));
            ////////////////////////////////////////////////////
            SetMaterialFallbackValue(avatar, true);
            if (!isModular)
            {
                descriptor.gameObject.SetActive(false);

                var newDesriptor = avatar.transform.GetComponentInChildren<ShellProtector>(true).gameObject;
                var tester = newDesriptor.AddComponent<ShellProtectorTester>();
                tester.lang = lang;
                tester.lang_idx = lang_idx;
                tester.protector = this;
                tester.user_key_length = key_size;
                Selection.activeObject = tester;

                SetAnimations(avatar, true);
                ObfuscateBlendShape(avatar, false);
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
                fx = AnimatorManager.DuplicateAnimator(av3.baseAnimationLayers[4].animatorController, Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString()));
            else
                fx = av3.baseAnimationLayers[4].animatorController as AnimatorController;

            av3.baseAnimationLayers[4].animatorController = fx;
            string animation_dir = Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "animations");

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
            DestroyImmediate(avatar.GetComponentInChildren<ShellProtector>(true));
        }

        public void ChangeMaterialsInAnims(GameObject avatar, bool clone)
        {
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            var fx = av3.baseAnimationLayers[4].animatorController as AnimatorController;
            string animationDir = Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "animations");

            AnimatorManager animManager = new AnimatorManager();
            foreach (var pair in encryptedMaterials)
            {
                Debug.LogFormat("{0}, {1}", pair.Key.name, pair.Value.name);
                animManager.ChangeAnimationMaterial(fx, pair.Key, pair.Value, clone, animationDir);
            }

#if MODULAR
            if(clone)
            {
                var maMergeAnims = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                foreach(var maMergeAnim in maMergeAnims)
                {
                    foreach (var pair in encryptedMaterials)
                    {
                        animManager.ChangeAnimationMaterial(maMergeAnim.animator as AnimatorController, pair.Key, pair.Value, clone, animationDir);
                    }
                }
            }
#endif
        }

        public VRCExpressionParameters GetParameter()
        {
            var av3 = descriptor;
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

        public void ObfuscateBlendShape(GameObject avatar, bool modular)
        {
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            AnimatorController fx = Getfx(avatar);
            string animDir = Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "animations");

            Obfuscator obfuscator = new Obfuscator();
            obfuscator.clone = !modular;
            obfuscator.bPreserveMMD = bPreserveMMD;

            var childRenderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>();

#if MODULAR
            //Check localblendshape is empty in MA Blendshape Sync
            var maBlendshapeSyncs = avatar.GetComponentsInChildren<ModularAvatarBlendshapeSync>(true);
            foreach (var maBlendshapeSync in maBlendshapeSyncs)
            {
                for (int i = 0; i < maBlendshapeSync.Bindings.Count; ++i)
                {
                    var binding = maBlendshapeSync.Bindings[i];
                    if (binding.LocalBlendshape == null || binding.LocalBlendshape == "")
                        binding.LocalBlendshape = string.Copy(binding.Blendshape);

                    maBlendshapeSync.Bindings[i] = binding;
                }
            }
#endif
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
                Mesh newMesh = obfuscator.ObfuscateBlendShapeMesh(mesh, Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString()));
                selectRenderer.sharedMesh = newMesh;

                ////////Change renderer component shape keys////////
                List<float> weights = new List<float>();
                for (int i = 0; i < newMesh.blendShapeCount; ++i)
                {
                    weights.Add(selectRenderer.GetBlendShapeWeight(i));
                    selectRenderer.SetBlendShapeWeight(i, 0.0f);
                }
                var obList = obfuscator.GetObfuscatedBlendShapeIndex();
                for (int i = 0; i < newMesh.blendShapeCount; ++i)
                {
                    selectRenderer.SetBlendShapeWeight(i, weights[obList[i]]);
                }
                /////////////////////////////////
#if MODULAR
                //Change MA Blendshape Sync component
                foreach (var maBlendshapeSync in maBlendshapeSyncs)
                {
                    for (int i = 0; i < maBlendshapeSync.Bindings.Count; ++i)
                    {
                        var binding = maBlendshapeSync.Bindings[i];

                        GameObject targetObject = binding.ReferenceMesh.Get(maBlendshapeSync);
                        SkinnedMeshRenderer targetRenderer = targetObject.GetComponent<SkinnedMeshRenderer>();
                        SkinnedMeshRenderer syncRenderer = maBlendshapeSync.GetComponent<SkinnedMeshRenderer>();

                        if (targetRenderer == null)
                            continue;
                        if (targetRenderer == selectRenderer)
                        {
                            string obfuscatedShape = obfuscator.GetOriginalBlendShapeName(binding.Blendshape);
                            if (obfuscatedShape != null)
                                binding.Blendshape = obfuscatedShape;
                        }

                        if (syncRenderer == null)
                            continue;
                        if (syncRenderer == selectRenderer)
                        {
                            string obfuscatedShape = obfuscator.GetOriginalBlendShapeName(binding.LocalBlendshape);
                            if (obfuscatedShape != null)
                                binding.LocalBlendshape = obfuscator.GetOriginalBlendShapeName(binding.LocalBlendshape);
                        }

                        maBlendshapeSync.Bindings[i] = binding;
                    }
                }
#endif
                obfuscator.ObfuscateBlendshapeInAnim(fx, selectRenderer.gameObject, animDir);
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

        public void DeleteEncyprtedFolders()
        {
            if (!Directory.Exists(asset_dir))
            {
                Debug.LogError($"The specified path does not exist: {asset_dir}");
            }
            else
            {
                string[] directories = Directory.GetDirectories(asset_dir);
                int deletedCount = 0;

                foreach (string dir in directories)
                {
                    string folderName = Path.GetFileName(dir);
                    if (Regex.IsMatch(folderName, @"^-*\d+$"))
                    {
                        try
                        {
                            AssetDatabase.DeleteAsset(dir);
                            deletedCount++;
                            Debug.Log($"Deleted folder: {dir}");
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"Failed to delete folder {dir}: {e.Message}");
                        }
                    }
                }

                Debug.Log($"Deletion complete. {deletedCount} folders were deleted.");
                AssetDatabase.Refresh();
            }
        }

        public int GetDefaultFilter()
        {
            return filter;
        }
    }
}
#endif