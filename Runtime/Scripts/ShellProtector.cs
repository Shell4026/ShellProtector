#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor.Animations;
using UnityEditor;
using UnityEngine.Serialization;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

#if MODULAR
using nadena.dev.modular_avatar.core;
#endif

namespace Shell.Protector
{
    public class ShellProtector : MonoBehaviour, IEditorOnly
    {
        [FormerlySerializedAs("gameobjectList")]
        [SerializeField]
        List<GameObject> _gameObjectList = new List<GameObject>();
        [FormerlySerializedAs("materialList")]
        [SerializeField]
        List<Material> _materialList = new List<Material>();
        [FormerlySerializedAs("obfuscationRenderers")]
        [SerializeField]
        List<SkinnedMeshRenderer> _obfuscationRenderers = new List<SkinnedMeshRenderer>();

        Injector _injector;
        readonly AssetManager _shaderManager = AssetManager.GetInstance();
        bool _initialized;
        string _packageAssetDir;

        enum Algorithm
        {
            Xxtea = 0,
            Chacha = 1
        }

        [FormerlySerializedAs("assetDir")]
        [SerializeField] string _assetDir = "Assets/ShellProtect";
        [FormerlySerializedAs("pwd")]
        [SerializeField] string _fixedPassword = "password";
        [FormerlySerializedAs("pwd2")]
        [SerializeField] string _userPassword = "pass";
        [FormerlySerializedAs("langIdx")]
        [SerializeField] int _languageIndex;
        [FormerlySerializedAs("lang")]
        [SerializeField] string _language = "kor";
        [FormerlySerializedAs("descriptor")]
        [SerializeField] VRCAvatarDescriptor _descriptor;

        public string AssetDir { get => _assetDir; set => _assetDir = value; }
        public string FixedPassword { get => _fixedPassword; set => _fixedPassword = value; }
        public string UserPassword { get => _userPassword; set => _userPassword = value; }
        public int LanguageIndex { get => _languageIndex; set => _languageIndex = value; }
        public string Language { get => _language; set => _language = value; }
        public VRCAvatarDescriptor Descriptor { get => _descriptor; set => _descriptor = value; }

        [Serializable]
        public class MatOption
        {
            [FormerlySerializedAs("active")]
            public bool Active = true;
            [FormerlySerializedAs("filter")]
            public int Filter = -1;
            [FormerlySerializedAs("fallback")]
            public int Fallback = -1;
            [FormerlySerializedAs("emissionEnc")]
            public bool EmissionEnc;
        }
        [Serializable]
        public class MaterialOptionPair
        {
            [FormerlySerializedAs("material")]
            public Material Material;
            [FormerlySerializedAs("option")]
            public MatOption Option;
        }

        [FormerlySerializedAs("matOptionSaved")]
        [SerializeField]
        List<MaterialOptionPair> _matOptionSaved = new List<MaterialOptionPair>();
        public Dictionary<Material, MatOption> MaterialOptions = new Dictionary<Material, MatOption>();

        EncryptedHistory _history;

        ShellProtectorBuildResult _buildResult = new ShellProtectorBuildResult();
        HashSet<GameObject> Meshes => _buildResult.Meshes;
        Dictionary<Material, Material> EncryptedMaterials => _buildResult.EncryptedMaterials;
        Dictionary<Texture2D, ShellProtectorProcessedTexture> ProcessedTextures => _buildResult.ProcessedTextures;

        [FormerlySerializedAs("rounds")]
        [SerializeField] uint _rounds = 20;
        [FormerlySerializedAs("filter")]
        [SerializeField] int _filter = 1;
        [FormerlySerializedAs("fallback")]
        [SerializeField] int _fallback = 5;
        [FormerlySerializedAs("algorithm")]
        [SerializeField] int _algorithm = 1;
#pragma warning disable CS0414
        [FormerlySerializedAs("keySizeIdx")]
        [SerializeField] int _keySizeIndex = 3;
#pragma warning restore CS0414
        [FormerlySerializedAs("keySize")]
        [SerializeField] int _keySize = 12;
        [FormerlySerializedAs("syncSize")]
        [SerializeField] int _syncSize = 1;
        [FormerlySerializedAs("deleteFolders")]
        [SerializeField] bool _deleteFolders = true;
        [FormerlySerializedAs("bUseSmallMipTexture")]
        [SerializeField] bool _useSmallMipTexture = true;

        [FormerlySerializedAs("bPreserveMMD")]
        [SerializeField] bool _preserveMmd = true;

        [FormerlySerializedAs("turnOnAllSafetyFallback")]
        [SerializeField] bool _turnOnAllSafetyFallback = true;

        public static readonly string[] FilterStrings = new string[2] { "Point", "Bilinear" };
        public static readonly string[] FallbackStrings = new string[8] { "white", "black", "4x4", "8x8", "16x16", "32x32", "64x64", "128x128" };

        Texture2D _fallbackWhite;
        Texture2D _fallbackBlack;

        string GetPackageAssetDir()
        {
            if (!string.IsNullOrEmpty(_packageAssetDir))
                return _packageAssetDir;

            MonoScript monoScript = MonoScript.FromMonoBehaviour(this);
            string scriptPath = AssetDatabase.GetAssetPath(monoScript);
            _packageAssetDir = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath));
            return _packageAssetDir;
        }

        string ResolveOutputAssetDir()
        {
            string packageDir = GetPackageAssetDir();
            if (string.IsNullOrEmpty(_assetDir) || _assetDir == "Assets/ShellProtect")
                _assetDir = packageDir;
            return _assetDir;
        }

        public void Init()
        {
            if (_initialized)
                return;

            HashSet<SkinnedMeshRenderer> rendererSet = new HashSet<SkinnedMeshRenderer>();
            Transform child = _descriptor.transform.Find("Body");
            if (child != null)
            {
                SkinnedMeshRenderer renderer = child.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null)
                {
                    Mesh mesh = renderer.sharedMesh;
                    if (mesh != null)
                    {
                        rendererSet.Add(renderer);
                    }

                }
            }
            foreach (var renderer in rendererSet)
            {
                _obfuscationRenderers.Add(renderer);
            }
            _initialized = true;
        }

        public void SyncMatOption()
        {
            foreach (var pair in _matOptionSaved)
            {
                if (pair.Material != null)
                    MaterialOptions[pair.Material] = pair.Option;
            }
        }
        public void SaveMatOption()
        {
            foreach (var pair in MaterialOptions)
            {
                _matOptionSaved.Add(new MaterialOptionPair { Material = pair.Key, Option = pair.Value });
            }
        }

        public byte[] GetKeyBytes()
        {
            return KeyGenerator.MakeKeyBytes(_fixedPassword, _userPassword, _keySize);
        }

        public GameObject DuplicateAvatar(GameObject avatar)
        {
            GameObject cpy = Instantiate(avatar);
            if (!avatar.name.Contains("_encrypted"))
                cpy.name = avatar.name + "_encrypted";
            return cpy;
        }

        public GameObject Encrypt(bool isModular = true)
        {
            return Encrypt(_useSmallMipTexture, isModular);
        }

        public GameObject Encrypt(bool useSmallMip, bool isModular = true)
        {
            var request = new ShellProtectorBuildRequest(this, _descriptor, useSmallMip, isModular);
            var result = new ShellProtectorPipeline().Encrypt(request, CreateSettings());
            ApplyBuildResult(result);
            return result.Avatar;
        }

        internal ShellProtectorBuildResult CurrentBuildResult => _buildResult;

        internal void ApplyBuildResult(ShellProtectorBuildResult result)
        {
            _buildResult = result ?? new ShellProtectorBuildResult();
        }

        internal ShellProtectorSettings CreateSettings()
        {
            return new ShellProtectorSettings
            {
                AssetDir = _assetDir,
                FixedPassword = _fixedPassword,
                UserPassword = _userPassword,
                Language = _language,
                LanguageIndex = _languageIndex,
                Rounds = _rounds,
                Filter = _filter,
                Fallback = _fallback,
                Algorithm = _algorithm,
                KeySize = _keySize,
                SyncSize = _syncSize,
                DeleteFolders = _deleteFolders,
                UseSmallMipTexture = _useSmallMipTexture,
                PreserveMMD = _preserveMmd,
                TurnOnAllSafetyFallback = _turnOnAllSafetyFallback
            };
        }

        internal void ApplySettings(ShellProtectorSettings settings)
        {
            if (settings == null)
                return;

            _assetDir = settings.AssetDir;
            _fixedPassword = settings.FixedPassword;
            _userPassword = settings.UserPassword;
            _language = settings.Language;
            _languageIndex = settings.LanguageIndex;
            _rounds = settings.Rounds;
            _filter = settings.Filter;
            _fallback = settings.Fallback;
            _algorithm = settings.Algorithm;
            _keySize = settings.KeySize;
            _syncSize = settings.SyncSize;
            _deleteFolders = settings.DeleteFolders;
            _useSmallMipTexture = settings.UseSmallMipTexture;
            _preserveMmd = settings.PreserveMMD;
            _turnOnAllSafetyFallback = settings.TurnOnAllSafetyFallback;
        }

        internal GameObject EncryptLegacy(bool useSmallMip, bool isModular = true)
        {
            Meshes.Clear();
            EncryptedMaterials.Clear();
            ProcessedTextures.Clear();

            SyncMatOption();

            string resourceDir = GetPackageAssetDir();
            _assetDir = ResolveOutputAssetDir();
            string avatarDir = Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString());
            _buildResult.AvatarDir = avatarDir;

            Debug.Log("AssetDir: " + _assetDir);

            if (_fallbackWhite == null)
                _fallbackWhite = AssetDatabase.LoadAssetAtPath(Path.Combine(resourceDir, "white.png"), typeof(Texture2D)) as Texture2D;
            if (_fallbackBlack == null)
                _fallbackBlack = AssetDatabase.LoadAssetAtPath(Path.Combine(resourceDir, "black.png"), typeof(Texture2D)) as Texture2D;

            if (_descriptor == null)
            {
                Debug.LogError("Can't find avatar descriptor!");
                return null;
            }

            _descriptor.gameObject.SetActive(true);
            Debug.Log("Key bytes: " + string.Join(", ", GetKeyBytes()));

            var materials = new List<Material>();
            foreach (var mat in GetMaterials())
            {
                if (CheckIsSupportedFormat(mat))
                {
                    materials.Add(mat);
                }
            }

            GameObject avatar;
            if (!isModular)
            {
                avatar = DuplicateAvatar(_descriptor.gameObject);
                Debug.Log("Duplicate avatar success.");
            }
            else
            {
                avatar = _descriptor.gameObject;
            }

            if (avatar == null)
            {
                Debug.LogError("Cannot create duplicated avatar!");
                return null;
            }
            byte[] keyBytes = GetKeyBytes();
            _buildResult.KeyBytes = keyBytes;

            CreateFolders();

            ///////////////////Select crypto algorithm/////////////////////
            IEncryptor encryptor = new XXTEA();
            if (_algorithm == (int)Algorithm.Xxtea)
            {
                XXTEA xxtea = new XXTEA();
                xxtea.Rounds = _rounds;
                encryptor = xxtea;
            }
            else if (_algorithm == (int)Algorithm.Chacha)
            {
                Chacha20 chacha = new Chacha20();
                byte[] hash1 = KeyGenerator.GetKeyHash(keyBytes, KeyGenerator.GenerateRandomString(chacha.Nonce.Length));
                Array.Copy(hash1, 0, chacha.Nonce, 0, chacha.Nonce.Length);
                encryptor = chacha;
            }
            ///////////////////////////////////////////////////////////////

            if (_history == null)
            {
                _history = AssetDatabase.LoadAssetAtPath(Path.Combine(_assetDir, "EncryptedHistory.asset"), typeof(EncryptedHistory)) as EncryptedHistory;
                if (_history == null)
                {
                    _history = ScriptableObject.CreateInstance<EncryptedHistory>();
                    AssetDatabase.CreateAsset(_history, Path.Combine(_assetDir, "EncryptedHistory.asset"));
                }
            }
            _history.LoadData();

            int progress = 0;
            int maxprogress = materials.Count;

            var mips = new Dictionary<int, Texture2D>();
            foreach (var mat in materials)
            {
                if (mat == null)
                    continue;
                int materialFilter = _filter;
#if UNITY_2022
                MatOption option = MaterialOptions.GetValueOrDefault(mat, null);
#else
                MatOption option = null;
                if (MaterialOptions.ContainsKey(mat))
                    option = MaterialOptions[mat];
#endif
                if (option != null)
                {
                    if (option.Active == false)
                    {
                        Debug.LogFormat("{0} : Skip", mat.name);
                        continue;
                    }
                    materialFilter = option.Filter;
                }

                EditorUtility.DisplayProgressBar("Encrypt...", "Encrypt Progress " + ++progress + " of " + maxprogress, (float)progress / (float)maxprogress);
                _injector = InjectorFactory.GetInjector(mat.shader);
                if (_injector == null)
                {
                    Debug.LogError(mat.shader + " is a unsupported shader! supported type:lilToon, poiyomi");
                    continue;
                }
                if (!ConditionCheck(mat))
                    continue;

                if (_shaderManager.IsPoiyomi(mat.shader))
                {
                    if (!_shaderManager.IsLockPoiyomi(mat))
                    {
                        _shaderManager.LockShader(mat);
                        Debug.LogFormat("Lock: {0} - {1}", mat.name, AssetDatabase.GetAssetPath(mat.shader));
                    }
                }

                Debug.LogFormat("{0} : Start encrypt...", mat.name);

                Texture2D mainTexture = (Texture2D)mat.mainTexture;
                _injector.Init(_descriptor.gameObject, mainTexture, keyBytes, _keySize, materialFilter, resourceDir, encryptor);

                int mipRefSize = Math.Max(mat.mainTexture.width, mat.mainTexture.height);
                if (!mips.ContainsKey(mipRefSize))
                {
                    Texture2D mipRef = GenerateMipRefTexture(Path.Combine(avatarDir, "tex", "mip_" + mipRefSize + ".asset"), mipRefSize, useSmallMip);
                    if (mipRef != null)
                        mips.Add(mipRefSize, mipRef);
                }

                TextureSettings.SetRWEnableTexture(mainTexture);
                TextureSettings.SetCrunchCompression(mainTexture, false);
                TextureSettings.SetGenerateMipmap(mainTexture, true);

                string encryptedShaderPath = Path.Combine(avatarDir, "shader", mat.GetInstanceID().ToString());

                var processedTextureResult = GenerateEncryptedTexture(avatarDir, mat, encryptor, keyBytes);
                if (!processedTextureResult.HasValue)
                    continue;
                ShellProtectorProcessedTexture processedTexture = processedTextureResult.Value;

                Texture2D encryptedTex1 = processedTexture.Encrypted.Texture1;
                Texture2D encryptedTex2 = processedTexture.Encrypted.Texture2;

                //////////////////////Inject shader///////////////////////
                ShellProtectorAuxiliaryTextures otherTex = GetLimOutlineTextures(mat);
                Shader encryptedShader = IsEncryptedBefore(mat.shader);
                if (encryptedShader == null)
                {
                    try
                    {
                        encryptedShader = _injector.Inject(
                            mat, 
                            Path.Combine(resourceDir, "Shader/ShellProtector.cginc"),
                            encryptedShaderPath,
                            encryptedTex1,
                            otherTex.LimTexture != null,
                            otherTex.LimTexture2 != null,
                            otherTex.OutlineTexture != null
                        );

                        Selection.activeObject = encryptedShader;
                        EditorApplication.ExecuteMenuItem("Assets/Reimport");
                        if (encryptedShader == null)
                        {
                            Debug.LogErrorFormat("{0}: Injection failed", mat.name);
                            continue;
                        }
                        _history.Save(mat.shader);
                    }
                    catch (UnityException e)
                    {
                        Debug.LogError(e.Message);
                        continue;
                    }
                }
                /////////////////////////////////////////////////////////
                string fallbackDir = Path.Combine(avatarDir, "tex", mainTexture.GetInstanceID() + "_fallback.asset");
                Texture2D fallback = GenerateFallbackTexture(fallbackDir, option, mainTexture, ref processedTexture);
                if (fallback == null)
                    Debug.LogErrorFormat("Failed to generate fallback texture: {0}", mainTexture.name);

                int maxSize = Math.Max(mainTexture.width, mainTexture.height);
                Texture2D mipTex = mips[maxSize];
                if (mipTex == null)
                    Debug.LogWarningFormat("mip_{0} is not exsist", maxSize);

                string encryptedMatPath = Path.Combine(avatarDir, "mat", mat.GetInstanceID() + "_encrypted.mat");
                GenerateEncryptedMaterial(encryptedMatPath, mat, encryptedShader, fallback, mipTex, otherTex, processedTexture, keyBytes, encryptor);
            } // Material loop
            EditorUtility.ClearProgressBar();

            ///////////////////////parameter////////////////////
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            av3.expressionParameters = ParameterManager.AddKeyParameter(av3.expressionParameters, _keySize, _syncSize);
            AssetDatabase.CreateAsset(av3.expressionParameters, Path.Combine(avatarDir, av3.expressionParameters.name + ".asset"));
            ////////////////////////////////////////////////////
            if (!isModular)
            {
                ReplaceMaterials(avatar);
                RemoveDuplicatedTextures(avatar);

                _descriptor.gameObject.SetActive(false);

                var newDesriptor = avatar.transform.GetComponentInChildren<ShellProtector>(true).gameObject;
                var tester = newDesriptor.AddComponent<ShellProtectorTester>();
                tester.Language = _language;
                tester.LanguageIndex = _languageIndex;
                tester.Protector = this;
                tester.UserKeyLength = _keySize;
                Selection.activeObject = tester;

#if MODULAR
                var maMergeAnims = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                foreach (var maMergeAnim in maMergeAnims)
                {
                    AnimatorController newAnim = AnimatorManager.DuplicateAnimator(maMergeAnim.animator, Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString()));
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

                        if (EncryptedMaterials.ContainsKey(mats[j]))
                        {
                            mats[j] = EncryptedMaterials[mats[j]];
                            Meshes.Add(renderers[i].gameObject);
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

                        if (EncryptedMaterials.ContainsKey(mats[j]))
                        {
                            mats[j] = EncryptedMaterials[mats[j]];
                            Meshes.Add(skinnedRenderers[i].gameObject);
                        }
                    }
                    skinnedRenderers[i].sharedMaterials = mats;
                }
            }
        }
        ShellProtectorAuxiliaryTextures GetLimOutlineTextures(Material mat)
        {
            ShellProtectorAuxiliaryTextures others = new ShellProtectorAuxiliaryTextures();
            if (_shaderManager.IsPoiyomi(mat.shader))
            {
                var tex_properties = mat.GetTexturePropertyNames();
                foreach (var t in tex_properties)
                {
                    if (t == "_RimTex")
                        others.LimTexture = (Texture2D)mat.GetTexture(t);
                    else if (t == "_Rim2Tex")
                        others.LimTexture2 = (Texture2D)mat.GetTexture(t);
                    else if (t == "_OutlineTexture")
                        others.OutlineTexture = (Texture2D)mat.GetTexture(t);
                }
            }
            else if (_shaderManager.IsLilToon(mat.shader))
            {
                var tex_properties = mat.GetTexturePropertyNames();
                foreach (var t in tex_properties)
                {
                    if (t == "_RimColorTex")
                        others.LimTexture = (Texture2D)mat.GetTexture(t);
                    else if (t == "_OutlineTex")
                        others.OutlineTexture = (Texture2D)mat.GetTexture(t);
                    else if (t == "_RimShadeMask")
                        others.LimShadeTexture = (Texture2D)mat.GetTexture(t);
                }
            }
            return others;
        }
        public void RemoveDuplicatedTextures(GameObject avatar)
        {
            string avatarDir = Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString());
            foreach (var mat in EncryptedMaterials.Values)
            {
                ShellProtectorAuxiliaryTextures otherTex = GetLimOutlineTextures(mat);

                foreach (var name in mat.GetTexturePropertyNames())
                {
                    if (mat.GetTexture(name) == null)
                        continue;
                    if (!(mat.GetTexture(name) is Texture2D))
                        continue;

                    if (ProcessedTextures.ContainsKey((Texture2D)mat.GetTexture(name)))
                    {
                        Texture2D mainTexture = (Texture2D)mat.GetTexture(name);
                        Texture2D encrypted0 = ProcessedTextures[(Texture2D)mat.GetTexture(name)].Encrypted.Texture1;

                        int idx = ProcessedTextures[(Texture2D)mat.GetTexture(name)].FallbackOptions.IndexOf(ProcessedTextures[(Texture2D)mat.GetTexture(name)].FallbackOptions.Max());
                        Texture2D bigFallbackTexture = ProcessedTextures[(Texture2D)mat.GetTexture(name)].Fallbacks[idx];

                        if (otherTex.LimTexture != null)
                        {
                            string texName = "";
                            if (_shaderManager.IsPoiyomi(mat.shader))
                                texName = "_RimTex";
                            else if (_shaderManager.IsLilToon(mat.shader))
                                texName = "_RimColorTex";

                            if (mainTexture == otherTex.LimTexture)
                                mat.SetTexture(texName, encrypted0);
                            else if (ProcessedTextures.ContainsKey(otherTex.LimTexture))
                                mat.SetTexture(texName, null);

                        }
                        if (otherTex.LimTexture2 != null) //only poiyomi
                        {
                            string texName = "";
                            if (_shaderManager.IsPoiyomi(mat.shader))
                                texName = "_Rim2Tex";

                            if (mainTexture == otherTex.LimTexture2)
                                mat.SetTexture(texName, encrypted0);
                            else if (ProcessedTextures.ContainsKey(otherTex.LimTexture2))
                                mat.SetTexture(texName, null);
                        }
                        if (otherTex.OutlineTexture != null)
                        {
                            string texName = "";
                            if (_shaderManager.IsPoiyomi(mat.shader))
                                texName = "_OutlineTexture";
                            else if (_shaderManager.IsLilToon(mat.shader))
                                texName = "_OutlineTex";

                            if (mainTexture == otherTex.OutlineTexture)
                                mat.SetTexture(texName, bigFallbackTexture);
                            else if (ProcessedTextures.ContainsKey(otherTex.OutlineTexture))
                                mat.SetTexture(texName, ProcessedTextures[otherTex.OutlineTexture].Fallbacks[0]);
                        }
                        if (otherTex.LimShadeTexture != null) //only liltoon
                        {
                            string texName = "_RimShadeMask";
                            if (mainTexture == otherTex.LimShadeTexture)
                                mat.SetTexture(texName, bigFallbackTexture);
                            else if (ProcessedTextures.ContainsKey(otherTex.LimShadeTexture))
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
                            if (ProcessedTextures.ContainsKey(tex))
                            {
                                int idx = ProcessedTextures[tex].FallbackOptions.IndexOf(ProcessedTextures[tex].FallbackOptions.Max());
                                Texture2D bigFallbackTexture = ProcessedTextures[tex].Fallbacks[idx];
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

                        Material tmp = null;
                        foreach (var name in mats[j].GetTexturePropertyNames())
                        {
                            if (mats[j].GetTexture(name) == null)
                                continue;
                            if (!(mats[j].GetTexture(name) is Texture2D))
                                continue;

                            if (ProcessedTextures.ContainsKey((Texture2D)mats[j].GetTexture(name)))
                            {
                                Texture2D mainTex = (Texture2D)mats[j].GetTexture(name);
                                int idx = ProcessedTextures[mainTex].FallbackOptions.IndexOf(ProcessedTextures[mainTex].FallbackOptions.Max());
                                Texture2D bigFallbackTexture = ProcessedTextures[mainTex].Fallbacks[idx];
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
                    skinnedRenderers[i].sharedMaterials = mats;
                }
            }
        }

        public void SetAnimations(GameObject avatar, bool clone)
        {
            var av3 = avatar.GetComponent<VRCAvatarDescriptor>();
            AnimatorController fx;
            if (clone)
                fx = AnimatorManager.DuplicateAnimator(av3.baseAnimationLayers[4].animatorController, Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString()));
            else
                fx = av3.baseAnimationLayers[4].animatorController as AnimatorController;

            av3.baseAnimationLayers[4].animatorController = fx;
            string animationDir = Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "animations");

            GameObject[] meshArray = new GameObject[Meshes.Count];
            Meshes.CopyTo(meshArray);
            AnimatorManager.CreateKeyAnimations(Path.Combine(GetPackageAssetDir(), "Animations"), animationDir, meshArray);
            AnimatorManager.AddKeyLayer(fx, animationDir, _keySize, _syncSize, 3.0f);

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
            string animationDir = Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "animations");

            AnimatorManager animManager = ScriptableObject.CreateInstance<AnimatorManager>();
            foreach (var pair in EncryptedMaterials)
            {
                Debug.LogFormat("{0}, {1}", pair.Key.name, pair.Value.name);
                animManager.ChangeAnimationMaterial(fx, pair.Key, pair.Value, clone, animationDir);
            }

#if MODULAR
            if (Clone)
            {
                var maMergeAnims = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                foreach (var maMergeAnim in maMergeAnims)
                {
                    if (maMergeAnim.animator == null)
                        continue;
                    foreach (var pair in EncryptedMaterials)
                    {
                        animManager.ChangeAnimationMaterial(maMergeAnim.animator as AnimatorController, pair.Key, pair.Value, clone, animationDir);
                    }
                }
            }
#endif
        }

        public VRCExpressionParameters GetParameter()
        {
            var av3 = _descriptor;
            if (av3 == null)
                return null;
            return av3.expressionParameters;
        }

        public static AnimatorController GetFx(GameObject avatar)
        {
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (av3 == null)
                return null;
            return av3.baseAnimationLayers[4].animatorController as AnimatorController;
        }

        public void ObfuscateBlendShape(GameObject avatar, bool clone)
        {
            // Clone true = Manual encrypt
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            AnimatorController fx = GetFx(avatar);
            string animDir = Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "animations");

            Obfuscator obfuscator = ScriptableObject.CreateInstance<Obfuscator>();
            obfuscator.Clone = clone;
            obfuscator.PreserveMmd = _preserveMmd;

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
            foreach (var renderer in _obfuscationRenderers)
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
                Mesh newMesh = obfuscator.ObfuscateBlendShapeMesh(mesh, Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString()));
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

                if (Clone)
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

        public int GetEncryptedFoldersCount()
        {
            if (!Directory.Exists(_assetDir))
            {
                Debug.LogError($"The specified path does not exist: {_assetDir}");
                return 0;
            }
            else
            {
                string[] directories = Directory.GetDirectories(_assetDir);
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
            AssetDatabase.DeleteAsset(Path.Combine(_assetDir, "EncryptedHistory.asset"));

            if (!Directory.Exists(_assetDir))
            {
                Debug.LogError($"The specified path does not exist: {_assetDir}");
            }
            else
            {
                string[] directories = Directory.GetDirectories(_assetDir);
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
            return _filter;
        }
        public int GetDefaultFallback()
        {
            return _fallback;
        }
        public int GetKeySize()
        {
            return _keySize;
        }

        public void ResetMaterialOptions()
        {
            _matOptionSaved.Clear();
            MaterialOptions.Clear();
        }

        public Shader IsEncryptedBefore(Shader shader)
        {
            if (_history == null)
            {
                _history = AssetDatabase.LoadAssetAtPath(Path.Combine(_assetDir, "EncryptedHistory.asset"), typeof(EncryptedHistory)) as EncryptedHistory;
                if (_history == null)
                {
                    _history = ScriptableObject.CreateInstance<EncryptedHistory>();
                    AssetDatabase.CreateAsset(_history, Path.Combine(_assetDir, "EncryptedHistory.asset"));
                }
            }
            _history.LoadData();
            return _history.IsEncryptedBefore(shader);
        }

        public static int GetRequiredSwitchCount(int keyLength, int syncSize)
        {
            keyLength /= syncSize;
            return Mathf.CeilToInt(Mathf.Log(keyLength, 2));
        }
        bool ConditionCheck(Material mat)
        {
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
            if (mat.mainTexture.width % 2 != 0 || mat.mainTexture.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", mat.mainTexture.name);
                return false;
            }
            if (_injector.WasInjected(mat.shader))
            {
                Debug.LogWarning(mat.name + ": The shader is already encrypted.");
                return false;
            }
            var av3 = _descriptor.gameObject.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (av3 == null)
            {
                Debug.LogError(_descriptor.gameObject.name + ": can't find VRCAvatarDescriptor!");
                return false;
            }
            if (av3.expressionParameters == null)
            {
                Debug.LogError(_descriptor.gameObject.name + ": can't find expressionParmeters!");
                return false;
            }
            return true;
        }
        public List<Material> GetMaterials()
        {
            List<Material> materials = new List<Material>();
            foreach (GameObject g in _gameObjectList)
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

            return materials.Concat(_materialList).Distinct().ToList();
        }
        bool CheckIsSupportedFormat(Material mat)
        {
            if (!TextureEncryptManager.IsSupportedFormat(mat))
            {
                if (mat.mainTexture != null)
                {
                    Debug.LogWarningFormat("{0} : is unsupported format", mat.mainTexture.name);
                }
                return false;
            }
            return true;
        }
        void CreateFolders()
        {
            if (!AssetDatabase.IsValidFolder(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString())))
            {
                AssetDatabase.CreateFolder(_assetDir, _descriptor.gameObject.GetInstanceID().ToString());
            }
            else
            {
                if (_deleteFolders)
                {
                    AssetDatabase.DeleteAsset(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "animations"));
                    AssetDatabase.DeleteAsset(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "mat"));
                    AssetDatabase.DeleteAsset(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "shader"));
                    AssetDatabase.DeleteAsset(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "tex"));
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!AssetDatabase.IsValidFolder(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "tex")))
                AssetDatabase.CreateFolder(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString()), "tex");
            if (!AssetDatabase.IsValidFolder(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "mat")))
                AssetDatabase.CreateFolder(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString()), "mat");
            if (!AssetDatabase.IsValidFolder(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "shader")))
                AssetDatabase.CreateFolder(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString()), "shader");
            if (!AssetDatabase.IsValidFolder(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString(), "animations")))
                AssetDatabase.CreateFolder(Path.Combine(_assetDir, _descriptor.gameObject.GetInstanceID().ToString()), "animations");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        Texture2D GenerateMipRefTexture(string outputDir, int size, bool useSmallMip)
        {
            var mip = TextureEncryptManager.GenerateRefMipmap(size, size, useSmallMip);
            if (mip == null)
                Debug.LogErrorFormat("{0} : Can't generate mip tex{1}.", outputDir, size);
            else
            {
                AssetDatabase.CreateAsset(mip, outputDir);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return mip;
        }
        ShellProtectorProcessedTexture? GenerateEncryptedTexture(string avatarDir, Material mat, IEncryptor encryptor, byte[] keyBytes)
        {
            Texture2D mainTexture = (Texture2D)mat.mainTexture;

            string texPath1 = Path.Combine(avatarDir, "tex", mainTexture.GetInstanceID() + "_encrypt.asset");
            string texPath2 = Path.Combine(avatarDir, "tex", mainTexture.GetInstanceID() + "_encrypt2.asset");

            bool processed = ProcessedTextures.ContainsKey(mainTexture);
            ShellProtectorProcessedTexture processedTexture;
            if (processed)
                processedTexture = ProcessedTextures[mainTexture];
            else
            {
                processedTexture = new ShellProtectorProcessedTexture
                {
                    Encrypted = new EncryptResult(),
                    Fallbacks = new List<Texture2D>(),
                    FallbackOptions = new List<int>(),
                    Nonce = new byte[12]
                };
            }

            //Set chacha nonce
            if (_algorithm == (int)Algorithm.Chacha)
            {
                Chacha20 chacha = encryptor as Chacha20;
                if (!processed)
                {
                    byte[] hashMat = KeyGenerator.GetHash(mat.GetInstanceID());
                    for (int i = 0; i < chacha.Nonce.Length; ++i)
                        chacha.Nonce[i] ^= hashMat[i];
                    Array.Copy(chacha.Nonce, 0, processedTexture.Nonce, 0, processedTexture.Nonce.Length);
                }
                else
                {
                    byte[] nonce = ProcessedTextures[mainTexture].Nonce;
                    Array.Copy(nonce, 0, chacha.Nonce, 0, chacha.Nonce.Length);
                }
            }

            if (!processed)
            {
                EncryptResult encryptResult;
                try
                {
                    encryptResult = TextureEncryptManager.EncryptTexture(mainTexture, keyBytes, encryptor);
                }
                catch (ArgumentException e)
                {
                    Debug.LogErrorFormat("{0} : ArgumentException - {1}", mainTexture.name, e.Message);
                    return null;
                }
                AssetDatabase.CreateAsset(encryptResult.Texture1, texPath1);
                if (encryptResult.Texture2 != null)
                    AssetDatabase.CreateAsset(encryptResult.Texture2, texPath2);

                processedTexture.Encrypted = encryptResult;

                ProcessedTextures.Add(mainTexture, processedTexture);
            }

            return processedTexture;
        }
        Texture2D GenerateFallbackTexture(string outputDir, MatOption option, Texture2D mainTexture, ref ShellProtectorProcessedTexture processedTexture)
        {
            int fallbackOption = _fallback;
            if (option != null)
                fallbackOption = option.Fallback;

            int idx = processedTexture.FallbackOptions.FindIndex(option => option == fallbackOption);
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
                    fallback = TextureEncryptManager.GenerateFallback(mainTexture, fallbackSize);
                    if (fallback != null)
                    {
                        processedTexture.Fallbacks.Add(fallback);
                        processedTexture.FallbackOptions.Add(fallbackOption);
                        AssetDatabase.CreateAsset(fallback, outputDir);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }
                else
                {
                    switch (fallbackSize)
                    {
                        case 0:
                            processedTexture.Fallbacks.Add(_fallbackWhite);
                            processedTexture.FallbackOptions.Add(fallbackOption);
                            fallback = _fallbackWhite;
                            break;
                        case 1:
                            processedTexture.Fallbacks.Add(_fallbackBlack);
                            processedTexture.FallbackOptions.Add(fallbackOption);
                            fallback = _fallbackBlack;
                            break;
                    }
                }
            }
            else
                fallback = processedTexture.Fallbacks[idx];

            return fallback;
        }
        Material GenerateEncryptedMaterial(string outputDir, Material mat, Shader encryptedShader, Texture2D fallback, Texture2D mip, ShellProtectorAuxiliaryTextures otherTex, ShellProtectorProcessedTexture processedTexture, byte[] keyBytes, IEncryptor encryptor)
        {
            Material newMat = new Material(mat.shader);
            newMat.CopyPropertiesFromMaterial(mat);
            newMat.shader = encryptedShader;
            var originalTex = (Texture2D)newMat.mainTexture;
            newMat.mainTexture = fallback;

            Texture2D encryptedTex0 = processedTexture.Encrypted.Texture1;
            Texture2D encryptedTex1 = processedTexture.Encrypted.Texture2;

            newMat.SetTexture(ShellProtectorShaderProperties.MipTexture, mip);

            if (encryptedTex0 != null)
                newMat.SetTexture(ShellProtectorShaderProperties.EncryptTexture0, encryptedTex0);
            if (encryptedTex1 != null)
                newMat.SetTexture(ShellProtectorShaderProperties.EncryptTexture1, encryptedTex1);

            newMat.renderQueue = mat.renderQueue;
            if (_turnOnAllSafetyFallback)
                newMat.SetOverrideTag("VRCFallback", "Unlit");

            var (woffset, hoffset) = TextureEncryptManager.CalculateOffsets(originalTex);
            newMat.SetInteger(ShellProtectorShaderProperties.WidthOffset, woffset);
            newMat.SetInteger(ShellProtectorShaderProperties.HeightOffset, hoffset);
            for (int i = 0; i < keyBytes.Length; ++i)
                newMat.SetFloat(ShellProtectorShaderProperties.KeyPrefix + i, keyBytes[i]);

            if (_algorithm == (int)Algorithm.Chacha)
            {
                Chacha20 chacha = encryptor as Chacha20;
                newMat.SetInteger(ShellProtectorShaderProperties.Nonce0, (int)chacha.GetNonceUint3()[0]);
                newMat.SetInteger(ShellProtectorShaderProperties.Nonce1, (int)chacha.GetNonceUint3()[1]);
                newMat.SetInteger(ShellProtectorShaderProperties.Nonce2, (int)chacha.GetNonceUint3()[2]);
            }
            else if (_algorithm == (int)Algorithm.Xxtea)
            {
                newMat.SetInteger(ShellProtectorShaderProperties.Rounds, (int)_rounds);
            }

            var key = new byte[16];
            for (int i = 0; i < 16; i++)
                key[i] = keyBytes[i];

            uint hashMagic = (uint)mat.GetInstanceID();

            var hash = KeyGenerator.SimpleHash(key, hashMagic);
            newMat.SetInteger(ShellProtectorShaderProperties.HashMagic, (int)hashMagic);
            newMat.SetInteger(ShellProtectorShaderProperties.PasswordHash, (int)hash);

            _injector.SetKeywords(newMat, otherTex.LimTexture != null);

            AssetDatabase.CreateAsset(newMat, outputDir);
            Debug.LogFormat("{0} : create encrypted material : {1}", mat.name, AssetDatabase.GetAssetPath(newMat));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EncryptedMaterials.ContainsKey(mat))
                EncryptedMaterials.Add(mat, newMat);

            return newMat;
        }
    }
}
#endif
