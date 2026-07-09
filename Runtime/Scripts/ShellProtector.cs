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
        const string LegacyOutputDir = "Assets/ShellProtect";
        const string WrongRuntimeOutputDir = "Assets/ShellProtector/Runtime";
        const string DefaultOutputDir = "Assets/ShellProtector/Generated";

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
        readonly AssetWriter _assetWriter = new AssetWriter();
        bool _initialized;
        string _packageAssetDir;
        OutputPaths _outputPaths;

        enum Algorithm
        {
            Xxtea = 0,
            Chacha = 1
        }

        [FormerlySerializedAs("assetDir")]
        [SerializeField] string _assetDir = DefaultOutputDir;
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

        BuildResult _buildResult = new BuildResult();
        HashSet<GameObject> Meshes => _buildResult.Meshes;
        Dictionary<Material, Material> EncryptedMaterials => _buildResult.EncryptedMaterials;
        Dictionary<Texture2D, ProcessedTexture> ProcessedTextures => _buildResult.ProcessedTextures;

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
            _packageAssetDir = OutputPaths.Normalize(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(scriptPath))));
            return _packageAssetDir;
        }

        string GetRuntimeAssetDir()
        {
            return OutputPaths.Combine(GetPackageAssetDir(), "Runtime");
        }

        string ResolveOutputAssetDir()
        {
            string normalized = OutputPaths.Normalize(_assetDir).TrimEnd('/');
            if (string.IsNullOrEmpty(normalized) || normalized == LegacyOutputDir || normalized == WrongRuntimeOutputDir || normalized == GetRuntimeAssetDir())
                normalized = DefaultOutputDir;

            _assetDir = normalized;
            return _assetDir;
        }

        OutputPaths GetOutputPaths()
        {
            if (_outputPaths == null)
                _outputPaths = new OutputPaths(_assetDir, _descriptor != null ? _descriptor.gameObject : null);
            return _outputPaths;
        }

        OutputPaths EnsureOutputFolders()
        {
            _assetDir = ResolveOutputAssetDir();
            OutputPaths paths = GetOutputPaths();
            if (paths.Folders == null)
                paths.PrepareFolders(_assetWriter, false);
            return paths;
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
            var request = new BuildRequest(this, _descriptor, useSmallMip, isModular);
            var result = new Pipeline().Encrypt(request, CreateSettings());
            ApplyBuildResult(result);
            return result.Avatar;
        }

        internal BuildResult CurrentBuildResult => _buildResult;

        internal void ApplyBuildResult(BuildResult result)
        {
            _buildResult = result ?? new BuildResult();
        }

        internal BuildSettings CreateSettings()
        {
            return new BuildSettings
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

        internal void ApplySettings(BuildSettings settings)
        {
            if (settings == null)
                return;

            _assetDir = settings.AssetDir;
            _outputPaths = null;
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

            string resourceDir = GetRuntimeAssetDir();
            _assetDir = ResolveOutputAssetDir();
            _outputPaths = new OutputPaths(_assetDir, _descriptor != null ? _descriptor.gameObject : null);
            string avatarDir = _outputPaths.Avatar;
            _buildResult.AvatarDir = avatarDir;

            Debug.Log("AssetDir: " + _assetDir);

            if (_fallbackWhite == null)
                _fallbackWhite = AssetDatabase.LoadAssetAtPath(OutputPaths.Combine(resourceDir, "white.png"), typeof(Texture2D)) as Texture2D;
            if (_fallbackBlack == null)
                _fallbackBlack = AssetDatabase.LoadAssetAtPath(OutputPaths.Combine(resourceDir, "black.png"), typeof(Texture2D)) as Texture2D;

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
                _history = AssetDatabase.LoadAssetAtPath(_outputPaths.History(), typeof(EncryptedHistory)) as EncryptedHistory;
                if (_history == null)
                {
                    _history = ScriptableObject.CreateInstance<EncryptedHistory>();
                    _assetWriter.CreateAssetInFolder(_history, _outputPaths.Folders.RootGuid, _outputPaths.HistoryName());
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
                    Texture2D mipRef = GenerateMipRefTexture(_outputPaths.MipTextureName(mipRefSize), mipRefSize, useSmallMip);
                    if (mipRef != null)
                        mips.Add(mipRefSize, mipRef);
                }

                TextureSettings.SetRWEnableTexture(mainTexture);
                TextureSettings.SetCrunchCompression(mainTexture, false);
                TextureSettings.SetGenerateMipmap(mainTexture, true);

                string encryptedShaderFolderGuid = _outputPaths.EnsureShaderFolder(_assetWriter, mat);
                string encryptedShaderPath = _assetWriter.ResolveFolderPath(encryptedShaderFolderGuid);

                var processedTextureResult = GenerateEncryptedTexture(_outputPaths, mat, encryptor, keyBytes);
                if (!processedTextureResult.HasValue)
                    continue;
                ProcessedTexture processedTexture = processedTextureResult.Value;

                Texture2D encryptedTex1 = processedTexture.Encrypted.Texture1;
                Texture2D encryptedTex2 = processedTexture.Encrypted.Texture2;

                //////////////////////Inject shader///////////////////////
                AuxiliaryTextures otherTex = GetLimOutlineTextures(mat);
                Shader encryptedShader = IsEncryptedBefore(mat.shader);
                if (encryptedShader == null)
                {
                    try
                    {
                        encryptedShader = _injector.Inject(
                            mat, 
                            OutputPaths.Combine(resourceDir, "Shader/Protector.cginc"),
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
                Texture2D fallback = GenerateFallbackTexture(_outputPaths.FallbackTextureName(mainTexture), option, mainTexture, ref processedTexture);
                if (fallback == null)
                    Debug.LogErrorFormat("Failed to generate fallback texture: {0}", mainTexture.name);

                int maxSize = Math.Max(mainTexture.width, mainTexture.height);
                Texture2D mipTex = mips[maxSize];
                if (mipTex == null)
                    Debug.LogWarningFormat("mip_{0} is not exsist", maxSize);

                GenerateEncryptedMaterial(_outputPaths.EncryptedMaterialName(mat), mat, encryptedShader, fallback, mipTex, otherTex, processedTexture, keyBytes, encryptor);
            } // Material loop
            EditorUtility.ClearProgressBar();

            ///////////////////////parameter////////////////////
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            av3.expressionParameters = ParameterManager.AddKeyParameter(av3.expressionParameters, _keySize, _syncSize);
            _assetWriter.CreateAssetInFolder(av3.expressionParameters, _outputPaths.Folders.AvatarGuid, _outputPaths.ParametersName(av3.expressionParameters.name));
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
                    AnimatorController newAnim = AnimatorManager.DuplicateAnimator(maMergeAnim.animator, _outputPaths, _assetWriter);
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
            AvatarProcessor.ReplaceMaterials(avatar, _buildResult);
        }
        AuxiliaryTextures GetLimOutlineTextures(Material mat)
        {
            AuxiliaryTextures others = new AuxiliaryTextures();
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
            OutputPaths paths = EnsureOutputFolders();
            foreach (var mat in EncryptedMaterials.Values)
            {
                AuxiliaryTextures otherTex = GetLimOutlineTextures(mat);

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

            var duplicatedMaterials = new Dictionary<Material, Material>();
            bool changedFallbackMaterials = ReplaceProcessedTexturesWithFallbacks(avatar.GetComponentsInChildren<MeshRenderer>(true), paths, duplicatedMaterials);
            changedFallbackMaterials |= ReplaceProcessedTexturesWithFallbacks(avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true), paths, duplicatedMaterials);
            if (changedFallbackMaterials)
                _assetWriter.SaveAndRefresh();
        }

        bool ReplaceProcessedTexturesWithFallbacks<T>(IEnumerable<T> renderers, OutputPaths paths, Dictionary<Material, Material> duplicatedMaterials) where T : Renderer
        {
            bool changed = false;
            foreach (T renderer in renderers)
            {
                Material[] mats = renderer.sharedMaterials;
                if (mats == null)
                    continue;

                bool rendererChanged = false;
                for (int i = 0; i < mats.Length; ++i)
                {
                    Material sourceMaterial = mats[i];
                    if (sourceMaterial == null)
                        continue;

                    Material duplicatedMaterial = null;
                    foreach (string name in sourceMaterial.GetTexturePropertyNames())
                    {
                        Texture2D texture = sourceMaterial.GetTexture(name) as Texture2D;
                        if (texture == null || !ProcessedTextures.TryGetValue(texture, out ProcessedTexture processedTexture))
                            continue;

                        if (duplicatedMaterial == null)
                            duplicatedMaterial = GetOrCreateDuplicatedMaterial(sourceMaterial, paths, duplicatedMaterials);

                        duplicatedMaterial.SetTexture(name, GetLargestFallback(processedTexture));
                        EditorUtility.SetDirty(duplicatedMaterial);
                        changed = true;
                    }

                    if (duplicatedMaterial != null)
                    {
                        mats[i] = duplicatedMaterial;
                        rendererChanged = true;
                    }
                }

                if (rendererChanged)
                    renderer.sharedMaterials = mats;
            }

            return changed;
        }

        Material GetOrCreateDuplicatedMaterial(Material source, OutputPaths paths, Dictionary<Material, Material> duplicatedMaterials)
        {
            if (duplicatedMaterials.TryGetValue(source, out Material duplicatedMaterial))
                return duplicatedMaterial;

            string duplicatedPath = OutputPaths.Combine(_assetWriter.ResolveFolderPath(paths.Folders.MatGuid), paths.DuplicatedMaterialName(source));
            duplicatedMaterial = AssetDatabase.LoadAssetAtPath<Material>(duplicatedPath);
            if (duplicatedMaterial == null)
            {
                duplicatedMaterial = Instantiate(source);
                _assetWriter.CreateAssetInFolder(duplicatedMaterial, paths.Folders.MatGuid, paths.DuplicatedMaterialName(source));
            }

            duplicatedMaterials[source] = duplicatedMaterial;
            return duplicatedMaterial;
        }

        static Texture2D GetLargestFallback(ProcessedTexture processedTexture)
        {
            int idx = processedTexture.FallbackOptions.IndexOf(processedTexture.FallbackOptions.Max());
            return processedTexture.Fallbacks[idx];
        }

        public void SetAnimations(GameObject avatar, bool clone)
        {
            var av3 = avatar.GetComponent<VRCAvatarDescriptor>();
            AnimatorController fx;
            OutputPaths paths = EnsureOutputFolders();
            if (clone)
                fx = AnimatorManager.DuplicateAnimator(av3.baseAnimationLayers[4].animatorController, paths, _assetWriter);
            else
                fx = av3.baseAnimationLayers[4].animatorController as AnimatorController;

            av3.baseAnimationLayers[4].animatorController = fx;
            string animationDir = _assetWriter.ResolveFolderPath(paths.Folders.AnimGuid);

            GameObject[] meshArray = new GameObject[Meshes.Count];
            Meshes.CopyTo(meshArray);
            AnimatorManager.CreateKeyAnimations(OutputPaths.Combine(GetRuntimeAssetDir(), "Animations"), paths, _assetWriter, meshArray);
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
            OutputPaths paths = EnsureOutputFolders();

            AnimatorManager animManager = ScriptableObject.CreateInstance<AnimatorManager>();
            foreach (var pair in EncryptedMaterials)
            {
                Debug.LogFormat("{0}, {1}", pair.Key.name, pair.Value.name);
                animManager.ChangeAnimationMaterial(fx, pair.Key, pair.Value, clone, paths, _assetWriter);
            }

#if MODULAR
            if (clone)
            {
                var maMergeAnims = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                foreach (var maMergeAnim in maMergeAnims)
                {
                    if (maMergeAnim.animator == null)
                        continue;
                    foreach (var pair in EncryptedMaterials)
                    {
                        animManager.ChangeAnimationMaterial(maMergeAnim.animator as AnimatorController, pair.Key, pair.Value, clone, paths, _assetWriter);
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
            OutputPaths paths = EnsureOutputFolders();

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
                Mesh newMesh = obfuscator.ObfuscateBlendShapeMesh(mesh, paths, _assetWriter);
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

                if (clone)
                {
                    var maMergeAnims = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                    foreach (var maMergeAnim in maMergeAnims)
                    {
                        obfuscator.ObfuscateBlendshapeInAnim(maMergeAnim.animator as AnimatorController, selectRenderer.gameObject, paths, _assetWriter);
                    }
                }
#endif
                obfuscator.ObfuscateBlendshapeInAnim(fx, selectRenderer.gameObject, paths, _assetWriter);
                obfuscator.ChangeObfuscatedBlendShapeInDescriptor(av3);
                obfuscator.Clean();
            }
        }

        public int GetEncryptedFoldersCount()
        {
            _assetDir = ResolveOutputAssetDir();
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
                    if (IsGeneratedOutputFolder(dir))
                        deletedCount++;
                }
                return deletedCount;
            }
        }
        public void CleanEncrypted()
        {
            _assetDir = ResolveOutputAssetDir();
            AssetDatabase.DeleteAsset(OutputPaths.Combine(_assetDir, "EncryptedHistory.asset"));

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
                    if (IsGeneratedOutputFolder(dir))
                    {
                        try
                        {
                            AssetDatabase.DeleteAsset(OutputPaths.Normalize(dir));
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

        bool IsGeneratedOutputFolder(string path)
        {
            string normalized = OutputPaths.Normalize(path);
            string folderName = Path.GetFileName(normalized);
            if (Regex.IsMatch(folderName, @"^-*\d+$"))
                return true;

            return AssetDatabase.IsValidFolder(OutputPaths.Combine(normalized, OutputPaths.TexFolder)) ||
                   AssetDatabase.IsValidFolder(OutputPaths.Combine(normalized, OutputPaths.MatFolder)) ||
                   AssetDatabase.IsValidFolder(OutputPaths.Combine(normalized, OutputPaths.ShaderFolder)) ||
                   AssetDatabase.IsValidFolder(OutputPaths.Combine(normalized, OutputPaths.AnimFolder)) ||
                   AssetDatabase.IsValidFolder(OutputPaths.Combine(normalized, OutputPaths.MeshFolder));
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
                _history = AssetDatabase.LoadAssetAtPath(GetOutputPaths().History(), typeof(EncryptedHistory)) as EncryptedHistory;
                if (_history == null)
                {
                    _history = ScriptableObject.CreateInstance<EncryptedHistory>();
                    OutputPaths paths = GetOutputPaths();
                    if (paths.Folders == null)
                        paths.PrepareFolders(_assetWriter, false);
                    _assetWriter.CreateAssetInFolder(_history, paths.Folders.RootGuid, paths.HistoryName());
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
            OutputPaths paths = GetOutputPaths();
            paths.PrepareFolders(_assetWriter, _deleteFolders && AssetDatabase.IsValidFolder(paths.Avatar));
        }

        Texture2D GenerateMipRefTexture(string fileName, int size, bool useSmallMip)
        {
            var mip = TextureEncryptManager.GenerateRefMipmap(size, size, useSmallMip);
            if (mip == null)
                Debug.LogErrorFormat("{0} : Can't generate mip tex{1}.", fileName, size);
            else
            {
                _assetWriter.CreateAssetInFolder(mip, GetOutputPaths().Folders.TexGuid, fileName);
                _assetWriter.SaveAndRefresh();
            }
            return mip;
        }
        ProcessedTexture? GenerateEncryptedTexture(OutputPaths paths, Material mat, IEncryptor encryptor, byte[] keyBytes)
        {
            Texture2D mainTexture = (Texture2D)mat.mainTexture;

            string texName1 = paths.EncryptedTextureName(mainTexture, 0);
            string texName2 = paths.EncryptedTextureName(mainTexture, 2);

            bool processed = ProcessedTextures.ContainsKey(mainTexture);
            ProcessedTexture processedTexture;
            if (processed)
                processedTexture = ProcessedTextures[mainTexture];
            else
            {
                processedTexture = new ProcessedTexture
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
                _assetWriter.CreateAssetInFolder(encryptResult.Texture1, paths.Folders.TexGuid, texName1);
                if (encryptResult.Texture2 != null)
                    _assetWriter.CreateAssetInFolder(encryptResult.Texture2, paths.Folders.TexGuid, texName2);

                processedTexture.Encrypted = encryptResult;

                ProcessedTextures.Add(mainTexture, processedTexture);
            }

            return processedTexture;
        }
        Texture2D GenerateFallbackTexture(string fileName, MatOption option, Texture2D mainTexture, ref ProcessedTexture processedTexture)
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
                        _assetWriter.CreateAssetInFolder(fallback, GetOutputPaths().Folders.TexGuid, fileName);
                        _assetWriter.SaveAndRefresh();
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
        Material GenerateEncryptedMaterial(string fileName, Material mat, Shader encryptedShader, Texture2D fallback, Texture2D mip, AuxiliaryTextures otherTex, ProcessedTexture processedTexture, byte[] keyBytes, IEncryptor encryptor)
        {
            MaterialEncryptor materialEncryptor = new MaterialEncryptor(_assetWriter, _turnOnAllSafetyFallback, _algorithm, _rounds);
            Material newMat = materialEncryptor.CreateEncryptedMaterial(GetOutputPaths().Folders.MatGuid, fileName, mat, encryptedShader, fallback, mip, otherTex, processedTexture, keyBytes, encryptor, _injector);
            Debug.LogFormat("{0} : create encrypted material : {1}", mat.name, AssetDatabase.GetAssetPath(newMat));

            if (!EncryptedMaterials.ContainsKey(mat))
                EncryptedMaterials.Add(mat, newMat);

            return newMat;
        }
    }
}
#endif
