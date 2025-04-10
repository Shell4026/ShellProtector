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
#elif POIYOMI91
using Thry.ThryEditor;
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

        enum Algorithm
        {
            xxtea = 0,
            chacha = 1
        }

        public string asset_dir = "Assets/ShellProtect";
        public string pwd = "password"; // fixed password
        public string pwd2 = "pass"; // user password
        public int lang_idx = 0;
        public string lang = "kor";
        public VRCAvatarDescriptor descriptor;

        [Serializable]
        public class MatOption
        {
            public bool active = true;
            public int filter = -1;
            public int fallback = -1;
        }
        [Serializable]
        public class MaterialOptionPair
        {
            public Material material;
            public MatOption option;
        }

        [SerializeField]
        List<MaterialOptionPair> matOptionSaved = new List<MaterialOptionPair>();
        public Dictionary<Material, MatOption> matOptions = new Dictionary<Material, MatOption>();

        EncryptedHistory history;

        struct ProcessedTexture
        {
            public Texture2D encrypted0;
            public Texture2D encrypted1;
            public List<Texture2D> fallbacks;
            public List<int> fallbackOptions;
            public byte[] nonce;
        }
        struct OtherTextures
        {
            public Texture2D limTexture;
            public Texture2D limTexture2;
            public Texture2D outlineTexture;
            public Texture2D limShadeTexture;
        }

        //Must clear them before start encrypting//
        HashSet<GameObject> meshes = new HashSet<GameObject>();
        Dictionary<Material, Material> encryptedMaterials = new Dictionary<Material, Material>(); // original, encrypted
        Dictionary<Texture2D, ProcessedTexture> processedTextures = new Dictionary<Texture2D, ProcessedTexture>();
        //////////////////////////////////

        [SerializeField] uint rounds = 20;
        [SerializeField] int filter = 1;
        [SerializeField] int fallback = 5;
        [SerializeField] int algorithm = 1;
        [SerializeField] int key_size_idx = 3;
        [SerializeField] int key_size = 12;
        [SerializeField] float animation_speed = 5.0f;
        [SerializeField] bool delete_folders = true;
        [SerializeField] bool parameter_multiplexing = false;
        [SerializeField] bool bUseSmallMipTexture = true;

        [SerializeField] bool bPreserveMMD = true;

        [SerializeField] float fallbackTime = 5.0f;
        [SerializeField] bool turnOnAllSafetyFallback = true;

        public static readonly string[] filterStrings = new string[2] { "Point", "Bilinear" };
        public static readonly string[] fallbackStrings = new string[8] { "white", "black", "4x4", "8x8", "16x16", "32x32", "64x64", "128x128" };

        Texture2D fallbackWhite = null;
        Texture2D fallbackBlack = null;

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

        public void SyncMatOption()
        {
            foreach(var pair in matOptionSaved)
            {
                if (pair.material != null)
                    matOptions[pair.material] = pair.option;
            }
        }
        public void SaveMatOption()
        {
            foreach(var pair in matOptions)
            {
                matOptionSaved.Add(new MaterialOptionPair { material = pair.Key, option = pair.Value });
            }
        }

        public byte[] GetKeyBytes()
        {
            return KeyGenerator.MakeKeyBytes(pwd, pwd2, key_size);
        }
        public EncryptTexture GetEncryptTexture()
        {
            return encrypt;
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
#elif POIYOMI91
                    ShaderOptimizer.LockMaterials(new[] { mat });
#endif
                }
            }
            if (mat.mainTexture == null)
            {
                Debug.LogWarningFormat("{0} : The mainTexture is empty. it will be skip.", mat.name);
                return false;
            }
            if ((mat.mainTexture is Texture2D) == false)
            {
                Debug.LogErrorFormat("MainTexture in {0} is not texture2D", mat.name);
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
            if (av3.expressionParameters == null)
            {
                Debug.LogError(descriptor.gameObject.name + ": can't find expressionParmeters!");
                return false;
            }
            return true;
        }

        bool CheckTextureFormat(Material mat)
        {
            if (mat == null)
                return false;
            if (mat.mainTexture == null)
                return false;
            var textureFormat = ((Texture2D)mat.mainTexture).format;
            if (textureFormat != TextureFormat.DXT1 && textureFormat != TextureFormat.DXT5 &&
                textureFormat != TextureFormat.RGB24 && textureFormat != TextureFormat.RGBA32)
            {
                Debug.LogWarningFormat("{0} : is unsupported format", mat.mainTexture.name);
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

            SyncMatOption();

            MonoScript monoScript = MonoScript.FromMonoBehaviour(this);
            string script_path = AssetDatabase.GetAssetPath(monoScript);
            asset_dir = Path.GetDirectoryName(Path.GetDirectoryName(script_path));
            string avatarDir = Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString());

            Debug.Log("AssetDir: " + asset_dir);

            if (fallbackWhite == null)
                fallbackWhite = AssetDatabase.LoadAssetAtPath(Path.Combine(asset_dir, "white.png"), typeof(Texture2D)) as Texture2D;
            if (fallbackBlack == null)
                fallbackBlack = AssetDatabase.LoadAssetAtPath(Path.Combine(asset_dir, "black.png"), typeof(Texture2D)) as Texture2D;

            if (descriptor == null)
            {
                Debug.LogError("Can't find avatar descriptor!");
                return null;
            }

            descriptor.gameObject.SetActive(true);
            Debug.Log("Key bytes: " + string.Join(", ", GetKeyBytes()));

            var materials = new List<Material>();
            foreach (var mat in GetMaterials())
            {
                if (CheckTextureFormat(mat))
                {
                    materials.Add(mat);
                }
            }

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
            if (algorithm == (int)Algorithm.xxtea)
            {
                XXTEA xxtea = new XXTEA();
                xxtea.m_rounds = rounds;
                encryptor = xxtea;
            }
            else if(algorithm == (int)Algorithm.chacha) 
            {
                Chacha20 chacha = new Chacha20();
                byte[] hash1 = KeyGenerator.GetKeyHash(key_bytes, KeyGenerator.GenerateRandomString(chacha.nonce.Length));
                Array.Copy(hash1, 0, chacha.nonce, 0, chacha.nonce.Length);
                encryptor = chacha;
            }
            ///////////////////////////////////////////////////////////////

            if (history == null)
            {
                history = AssetDatabase.LoadAssetAtPath(Path.Combine(asset_dir, "EncryptedHistory.asset"), typeof(EncryptedHistory)) as EncryptedHistory;
                if (history == null)
                {
                    history = new EncryptedHistory();
                    AssetDatabase.CreateAsset(history, Path.Combine(asset_dir, "EncryptedHistory.asset"));
                }
            }
            history.LoadData();

            int progress = 0;
            int maxprogress = materials.Count;
            foreach (var mat in materials)
            {
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
                if (mat == null)
                    continue;

                EditorUtility.DisplayProgressBar("Encrypt...", "Encrypt Progress " + ++progress + " of " + maxprogress, (float)progress / (float)maxprogress);
                injector = InjectorFactory.GetInjector(mat.shader);
                if (injector == null)
                {
                    Debug.LogWarning(mat.shader + " is a unsupported shader! supported type:lilToon, poiyomi");
                    continue;
                }
                if (!ConditionCheck(mat))
                {
                    continue;
                }
                Debug.LogFormat("{0} : Start encrypt...", mat.name);

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
                TextureSettings.SetRWEnableTexture(main_texture);
                TextureSettings.SetCrunchCompression(main_texture, false);
                TextureSettings.SetGenerateMipmap(main_texture, true);

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
                        fallbacks = new List<Texture2D>(),
                        fallbackOptions = new List<int>(),
                        nonce = new byte[12]
                    };

                //Set chacha nonce
                if (algorithm == (int)Algorithm.chacha)
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
                    try
                    {
                        encrypted_tex = encrypt.TextureEncrypt(main_texture, key_bytes, encryptor);
                    }
                    catch(ArgumentException e)
                    {
                        Debug.LogErrorFormat("{0} : ArgumentException - {1}", main_texture.name, e.Message);
                        continue;
                    }
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

                //////////////////////Inject shader///////////////////////
                OtherTextures otherTex = GetLimOutlineTextures(mat);
                Shader encrypted_shader = IsEncryptedBefore(mat.shader);
                if (encrypted_shader == null)
                {
                    try
                    {
                        string decodeDir = "";
                        if (algorithm == (int)Algorithm.xxtea)
                            decodeDir = Path.Combine(asset_dir, "Decrypt.cginc");
                        else if (algorithm == (int)Algorithm.chacha)
                            decodeDir = Path.Combine(asset_dir, "DecryptChacha.cginc");

                        encrypted_shader = injector.Inject(mat, decodeDir, encrypted_shader_path, encrypted_tex[0], 
                            otherTex.limTexture != null, otherTex.limTexture2 != null, otherTex.outlineTexture != null);

                        Selection.activeObject = encrypted_shader;
                        EditorApplication.ExecuteMenuItem("Assets/Reimport");
                        if (encrypted_shader == null)
                        {
                            Debug.LogErrorFormat("{0}: Injection failed", mat.name);
                            continue;
                        }
                        history.Save(mat.shader);
                    }
                    catch (UnityException e)
                    {
                        Debug.LogError(e.Message);
                        continue;
                    }
                }
                #region FallbackTexture
                /////////////////Generate fallback/////////////////////
                string fallbackDir = Path.Combine(avatarDir, "tex", main_texture.GetInstanceID() + "_fallback.asset");
                int fallbackOption = this.fallback;
                if (option != null)
                    fallbackOption = option.fallback;

                int idx = processedTexture.fallbackOptions.FindIndex(option => option == fallbackOption);
                Texture2D fallback = null;
                if (idx == -1)
                {
                    int fallbackSize = 32;
                    switch (fallbackOption)
                    {
                        case 0: // white
                            fallbackSize = 0;
                            break;
                        case 1: // black
                            fallbackSize = 1;
                            break;
                        case 2:
                            fallbackSize = 4;
                            break;
                        case 3:
                            fallbackSize = 8;
                            break;
                        case 4:
                            fallbackSize = 16;
                            break;
                        case 5:
                            fallbackSize = 32;
                            break;
                        case 6:
                            fallbackSize = 64;
                            break;
                        case 7:
                            fallbackSize = 128;
                            break;
                    }
                    if (fallbackSize > 1)
                    {
                        fallback = encrypt.GenerateFallback(main_texture, fallbackSize);
                        if (fallback != null)
                        {
                            processedTexture.fallbacks.Add(fallback);
                            processedTexture.fallbackOptions.Add(fallbackOption);
                            AssetDatabase.CreateAsset(fallback, fallbackDir);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                        }
                    }
                    else
                    {
                        switch (fallbackSize)
                        {
                            case 0:
                                processedTexture.fallbacks.Add(fallbackWhite);
                                processedTexture.fallbackOptions.Add(fallbackOption);
                                fallback = fallbackWhite;
                                break;
                            case 1:
                                processedTexture.fallbacks.Add(fallbackBlack);
                                processedTexture.fallbackOptions.Add(fallbackOption);
                                fallback = fallbackBlack;
                                break;
                        }
                    }
                }
                else
                    fallback = processedTexture.fallbacks[idx];
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
                if (turnOnAllSafetyFallback)
                    new_mat.SetOverrideTag("VRCFallback", "Unlit");

                int woffset = 0;
                int hoffset = 0;
                if (main_texture.format == TextureFormat.DXT1 || main_texture.format == TextureFormat.DXT5)
                {
                    woffset = 13 - (int)Mathf.Log(main_texture.width, 2) - 1 + 2;
                    hoffset = 13 - (int)Mathf.Log(main_texture.height, 2) - 1 + 2;
                }
                else
                {
                    woffset = 13 - (int)Mathf.Log(main_texture.width, 2) - 1;
                    hoffset = 13 - (int)Mathf.Log(main_texture.height, 2) - 1;
                }
                new_mat.SetInteger("_Woffset", woffset);
                new_mat.SetInteger("_Hoffset", hoffset);
                for (int i = 0; i < 16 - key_size; ++i)
                    new_mat.SetFloat("_Key" + i, key_bytes[i]);

                if (algorithm == (int)Algorithm.chacha)
                {
                    Chacha20 chacha = encryptor as Chacha20;
                    new_mat.SetInteger("_Nonce0", (int)chacha.GetNonceUint3()[0]);
                    new_mat.SetInteger("_Nonce1", (int)chacha.GetNonceUint3()[1]);
                    new_mat.SetInteger("_Nonce2", (int)chacha.GetNonceUint3()[2]);
                }

                AssetDatabase.CreateAsset(new_mat, encrypted_mat_path);
                Debug.LogFormat("{0} : create encrypted material : {1}", mat.name, AssetDatabase.GetAssetPath(new_mat));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                //////////////////////////////////////////////////////
                #endregion

                if (!processed)
                    processedTextures.Add(main_texture, processedTexture);
                if (!encryptedMaterials.ContainsKey(mat))
                    encryptedMaterials.Add(mat, new_mat);
            } // Material loop
            EditorUtility.ClearProgressBar();

            ///////////////////////parameter////////////////////
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            av3.expressionParameters = ParameterManager.AddKeyParameter(av3.expressionParameters, key_size, parameter_multiplexing);
            AssetDatabase.CreateAsset(av3.expressionParameters, Path.Combine(avatarDir, av3.expressionParameters.name + ".asset"));
            ////////////////////////////////////////////////////
            if (!isModular)
            {
                ReplaceMaterials(avatar);
                RemoveDuplicatedTextures(avatar);
                SetMaterialFallbackValue(avatar, true);

                descriptor.gameObject.SetActive(false);

                var newDesriptor = avatar.transform.GetComponentInChildren<ShellProtector>(true).gameObject;
                var tester = newDesriptor.AddComponent<ShellProtectorTester>();
                tester.lang = lang;
                tester.lang_idx = lang_idx;
                tester.protector = this;
                tester.userKeyLength = key_size;
                Selection.activeObject = tester;

#if MODULAR
                var maMergeAnims = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                foreach (var maMergeAnim in maMergeAnims)
                {
                    AnimatorController newAnim = AnimatorManager.DuplicateAnimator(maMergeAnim.animator, Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString()));
                    maMergeAnim.animator = newAnim;
                }
#endif
                SetAnimations(avatar, true);
                ObfuscateBlendShape(avatar, true);
                ChangeMaterialsInAnims(avatar, true);
                CleanComponent(avatar);    
            }

            
            return avatar;
        }

        public void ReplaceMaterials(GameObject avatar)
        {
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

                        if (encryptedMaterials.ContainsKey(mats[j]))
                        {
                            mats[j] = encryptedMaterials[mats[j]];
                            meshes.Add(renderers[i].gameObject);
                        }
                    }
                    renderers[i].sharedMaterials = mats;
                }
            }
            var skinnedRenderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinnedRenderers != null)
            {
                for (int i = 0; i < skinnedRenderers.Length; ++i)
                {
                    var mats = skinnedRenderers[i].sharedMaterials;
                    if (mats == null)
                        continue;
                    for (int j = 0; j < mats.Length; ++j)
                    {
                        if (mats[j] == null)
                            continue;

                        if (encryptedMaterials.ContainsKey(mats[j]))
                        {
                            mats[j] = encryptedMaterials[mats[j]];
                            meshes.Add(skinnedRenderers[i].gameObject);
                        }
                    }
                    skinnedRenderers[i].sharedMaterials = mats;
                }
            }
        }
        OtherTextures GetLimOutlineTextures(Material mat)
        {
            OtherTextures others = new OtherTextures();
            if (shader_manager.IsPoiyomi(mat.shader))
            {
                var tex_properties = mat.GetTexturePropertyNames();
                foreach (var t in tex_properties)
                {
                    if (t == "_RimTex")
                        others.limTexture = (Texture2D)mat.GetTexture(t);
                    else if (t == "_Rim2Tex")
                        others.limTexture2 = (Texture2D)mat.GetTexture(t);
                    else if (t == "_OutlineTexture")
                        others.outlineTexture = (Texture2D)mat.GetTexture(t);
                }
            }
            else if (shader_manager.IslilToon(mat.shader))
            {
                var tex_properties = mat.GetTexturePropertyNames();
                foreach (var t in tex_properties)
                {
                    if (t == "_RimColorTex")
                        others.limTexture = (Texture2D)mat.GetTexture(t);
                    else if (t == "_OutlineTex")
                        others.outlineTexture = (Texture2D)mat.GetTexture(t);
                    else if (t == "_RimShadeMask")
                        others.limShadeTexture = (Texture2D)mat.GetTexture(t);
                }
            }
            return others;
        }
        public void RemoveDuplicatedTextures(GameObject avatar)
        {
            string avatarDir = Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString());
            foreach (var mat in encryptedMaterials.Values)
            {
                OtherTextures otherTex = GetLimOutlineTextures(mat);

                foreach (var name in mat.GetTexturePropertyNames())
                {
                    if (mat.GetTexture(name) == null)
                        continue;
                    if (!(mat.GetTexture(name) is Texture2D))
                        continue;

                    if (processedTextures.ContainsKey((Texture2D)mat.GetTexture(name)))
                    {
                        Texture2D mainTexture = (Texture2D)mat.GetTexture(name);
                        Texture2D encrypted0 = processedTextures[(Texture2D)mat.GetTexture(name)].encrypted0;
                        
                        int idx = processedTextures[(Texture2D)mat.GetTexture(name)].fallbackOptions.IndexOf(processedTextures[(Texture2D)mat.GetTexture(name)].fallbackOptions.Max());
                        Texture2D bigFallbackTexture = processedTextures[(Texture2D)mat.GetTexture(name)].fallbacks[idx];

                        if (otherTex.limTexture != null)
                        {
                            string texName = "";
                            if (shader_manager.IsPoiyomi(mat.shader))
                                texName = "_RimTex";
                            else if (shader_manager.IslilToon(mat.shader))
                                texName = "_RimColorTex";

                            if (mainTexture == otherTex.limTexture)
                                mat.SetTexture(texName, encrypted0);
                            else if (processedTextures.ContainsKey(otherTex.limTexture))
                                mat.SetTexture(texName, null);

                        }
                        if (otherTex.limTexture2 != null) //only poiyomi
                        {
                            string texName = "";
                            if (shader_manager.IsPoiyomi(mat.shader))
                                texName = "_Rim2Tex";

                            if (mainTexture == otherTex.limTexture2)
                                mat.SetTexture(texName, encrypted0);
                            else if (processedTextures.ContainsKey(otherTex.limTexture2))
                                mat.SetTexture(texName, null);
                        }
                        if (otherTex.outlineTexture != null)
                        {
                            string texName = "";
                            if (shader_manager.IsPoiyomi(mat.shader))
                                texName = "_OutlineTexture";
                            else if (shader_manager.IslilToon(mat.shader))
                                texName = "_OutlineTex";

                            if (mainTexture == otherTex.outlineTexture)
                                mat.SetTexture(texName, bigFallbackTexture);
                            else if (processedTextures.ContainsKey(otherTex.outlineTexture))
                                mat.SetTexture(texName, processedTextures[otherTex.outlineTexture].fallbacks[0]);
                        }
                        if (otherTex.limShadeTexture != null) //only liltoon
                        {
                            string texName = "_RimShadeMask";
                            if (mainTexture == otherTex.limShadeTexture)
                                mat.SetTexture(texName, bigFallbackTexture);
                            else if (processedTextures.ContainsKey(otherTex.limShadeTexture))
                                mat.SetTexture(texName, null);
                        }
                    }
                }
            } // Encrypted materials loop

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

                        Material tmp = null;
                        foreach (var name in mats[j].GetTexturePropertyNames())
                        {
                            if (mats[j].GetTexture(name) == null)
                                continue;
                            if (!(mats[j].GetTexture(name) is Texture2D))
                                continue;
                            Texture2D tex = (Texture2D)mats[j].GetTexture(name);
                            if (processedTextures.ContainsKey(tex))
                            {
                                int idx = processedTextures[tex].fallbackOptions.IndexOf(processedTextures[tex].fallbackOptions.Max());
                                Texture2D bigFallbackTexture = processedTextures[tex].fallbacks[idx];
                                if (tmp == null)
                                {
                                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(avatarDir, "mat", (mats[j].GetInstanceID() + "_duplicated.mat")));
                                    if (mat == null)
                                    {
                                        tmp = Instantiate(mats[j]);
                                        AssetDatabase.CreateAsset(tmp, Path.Combine(avatarDir, "mat", (mats[j].GetInstanceID() + "_duplicated.mat")));
                                        AssetDatabase.SaveAssets();
                                    }
                                    else
                                        tmp = mat;
                                }
                                tmp.SetTexture(name, bigFallbackTexture);
                            }
                        }
                        AssetDatabase.Refresh();
                        if (tmp != null)
                            mats[j] = tmp;
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

                        Material tmp = null;
                        foreach (var name in mats[j].GetTexturePropertyNames())
                        {
                            if (mats[j].GetTexture(name) == null)
                                continue;
                            if (!(mats[j].GetTexture(name) is Texture2D))
                                continue;

                            if (processedTextures.ContainsKey((Texture2D)mats[j].GetTexture(name)))
                            {
                                Texture2D mainTex = (Texture2D)mats[j].GetTexture(name);
                                int idx = processedTextures[mainTex].fallbackOptions.IndexOf(processedTextures[mainTex].fallbackOptions.Max());
                                Texture2D bigFallbackTexture = processedTextures[mainTex].fallbacks[idx];
                                if (tmp == null)
                                {
                                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(avatarDir, "mat", (mats[j].GetInstanceID() + "_duplicated.mat")));
                                    if (mat == null)
                                    {
                                        tmp = Instantiate(mats[j]);
                                        AssetDatabase.CreateAsset(tmp, Path.Combine(avatarDir, "mat", (mats[j].GetInstanceID() + "_duplicated.mat")));
                                        AssetDatabase.SaveAssets();
                                    }
                                    else
                                        tmp = mat;
                                }
                                tmp.SetTexture(name, bigFallbackTexture);
                            }
                        }
                        AssetDatabase.Refresh();
                        if (tmp != null)
                            mats[j] = tmp;
                    }
                    skinned_renderers[i].sharedMaterials = mats;
                }
            }
        }
        public void SetAnimations(GameObject avatar, bool clone)
        {
            var av3 = avatar.GetComponent<VRCAvatarDescriptor>();
            AnimatorController fx;
            if (clone)
                fx = AnimatorManager.DuplicateAnimator(av3.baseAnimationLayers[4].animatorController, Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString()));
            else
                fx = av3.baseAnimationLayers[4].animatorController as AnimatorController;

            av3.baseAnimationLayers[4].animatorController = fx;
            string animationDir = Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "animations");

            GameObject[] meshArray = new GameObject[meshes.Count];
            meshes.CopyTo(meshArray);
            AnimatorManager.CreateKeyAniamtions(Path.Combine(asset_dir, "Animations"), animationDir, meshArray);
            var fallbackOnAnim = AnimatorManager.CreateFallbackAniamtions(Path.Combine(asset_dir, "Animations", "FallbackOn.anim"), animationDir, meshArray, false);
            var fallbackOffAnim = AnimatorManager.CreateFallbackAniamtions(Path.Combine(asset_dir, "Animations", "FallbackOff.anim"), animationDir, meshArray, true);
            AnimatorManager.AddKeyLayer(fx, animationDir, key_size, animation_speed, parameter_multiplexing);
            AnimatorManager.AddFallbackLayer(fx, fallbackOnAnim, fallbackOffAnim, fallbackTime);

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
            if (clone)
            {
                var maMergeAnims = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                foreach (var maMergeAnim in maMergeAnims)
                {
                    if (maMergeAnim.animator == null)
                        continue;
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

        public void ObfuscateBlendShape(GameObject avatar, bool bClone)
        {
            // bClone true = Manual encrypt
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            AnimatorController fx = Getfx(avatar);
            string animDir = Path.Combine(asset_dir, descriptor.gameObject.GetInstanceID().ToString(), "animations");

            Obfuscator obfuscator = new Obfuscator();
            obfuscator.clone = bClone;
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

                if(bClone)
                {
                    var maMergeAnims = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                    foreach (var maMergeAnim in maMergeAnims)
                    {
                        obfuscator.ObfuscateBlendshapeInAnim(maMergeAnim.animator as AnimatorController, selectRenderer.gameObject, animDir);
                    }
                }
#endif
                obfuscator.ObfuscateBlendshapeInAnim(fx, selectRenderer.gameObject, animDir);
                obfuscator.ChangeObfuscatedBlendShapeInDescriptor(av3);
                obfuscator.Clean();
            }
        }

        public void SetMaterialFallbackValue(GameObject avatar, bool fallback)
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
                        if (mat.name.Contains("_encrypted") || mat.name.Contains("_duplicated"))
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
                        if (mat.name.Contains("_encrypted") || mat.name.Contains("_duplicated"))
                        {
                            mat.SetFloat("_fallback", fallback == true ? 1.0f : 0.0f);
                        }
                    }
                }
            }
        }

        public int GetEncyryptedFoldersCount()
        {
            if (!Directory.Exists(asset_dir))
            {
                Debug.LogError($"The specified path does not exist: {asset_dir}");
                return 0;
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
                        deletedCount++;
                    }
                }
                return deletedCount;
            }
        }
        public void CleanEncrypted()
        {
            AssetDatabase.DeleteAsset(Path.Combine(asset_dir, "EncryptedHistory.asset"));

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
        public int GetDefaultFallback()
        {
            return fallback;
        }
        public int GetKeySize()
        {
            return key_size;
        }

        public void ResetMaterialOptions()
        {
            matOptionSaved.Clear();
            matOptions.Clear();
        }

        public Shader IsEncryptedBefore(Shader shader)
        {
            if (history == null)
            {
                history = AssetDatabase.LoadAssetAtPath(Path.Combine(asset_dir, "EncryptedHistory.asset"), typeof(EncryptedHistory)) as EncryptedHistory;
                if (history == null)
                {
                    history = new EncryptedHistory();
                    AssetDatabase.CreateAsset(history, Path.Combine(asset_dir, "EncryptedHistory.asset"));
                }
            }
            history.LoadData();
            return history.IsEncryptedBefore(shader);
        }
    }
}
#endif