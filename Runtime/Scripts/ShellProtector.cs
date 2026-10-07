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

        // The underscored names are those of 2.6.0 to 2.7.0.
        [FormerlySerializedAs("gameobjectList")]
        [FormerlySerializedAs("_gameObjectList")]
        [SerializeField]
        List<GameObject> gameObjectList = new List<GameObject>();
        [FormerlySerializedAs("_materialList")]
        [SerializeField]
        List<Material> materialList = new List<Material>();
        [FormerlySerializedAs("_obfuscationRenderers")]
        [SerializeField]
        List<SkinnedMeshRenderer> obfuscationRenderers = new List<SkinnedMeshRenderer>();

        Injector injector;
        readonly AssetManager shaderManager = AssetManager.GetInstance();
        readonly AssetWriter assetWriter = new AssetWriter();
        string packageAssetDir;
        OutputPaths outputPaths;

        enum Algorithm
        {
            Xxtea = 0,
            Chacha = 1
        }

        [FormerlySerializedAs("_assetDir")]
        [SerializeField] string assetDir = DefaultOutputDir;
        [FormerlySerializedAs("pwd")]
        [FormerlySerializedAs("_fixedPassword")]
        [SerializeField] string fixedPassword = "password";
        [FormerlySerializedAs("pwd2")]
        [FormerlySerializedAs("_userPassword")]
        [SerializeField] string userPassword = "pass";
        // Per-avatar salt for UserKey. It stays the same across builds so the OSC app keeps finding it.
        [FormerlySerializedAs("_parameterSalt")]
        [SerializeField] string parameterSalt = "";
        [FormerlySerializedAs("langIdx")]
        [FormerlySerializedAs("_languageIndex")]
        [SerializeField] int languageIndex;
        [FormerlySerializedAs("lang")]
        [FormerlySerializedAs("_language")]
        [SerializeField] string language = "kor";
        [FormerlySerializedAs("_descriptor")]
        [SerializeField] VRCAvatarDescriptor descriptor;

        public string AssetDir { get => assetDir; set => assetDir = value; }
        public string FixedPassword { get => fixedPassword; set => fixedPassword = value; }
        public string UserPassword { get => userPassword; set => userPassword = value; }
        public string ParameterSalt { get => parameterSalt; set => parameterSalt = value; }
        public int LanguageIndex { get => languageIndex; set => languageIndex = value; }
        public string Language { get => language; set => language = value; }
        public VRCAvatarDescriptor Descriptor { get => descriptor; set => descriptor = value; }

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
            // Explicit opt-in per shader slot. Legacy EmissionEnc is not migrated.
            public int EmissionMask = 0;
        }
        [Serializable]
        public class MaterialOptionPair
        {
            [FormerlySerializedAs("Material")]
            public Material material;
            [FormerlySerializedAs("Option")]
            public MatOption option;
        }

        [FormerlySerializedAs("_matOptionSaved")]
        [SerializeField]
        List<MaterialOptionPair> matOptionSaved = new List<MaterialOptionPair>();
        public Dictionary<Material, MatOption> materialOptions = new Dictionary<Material, MatOption>();

        EncryptedHistory history;

        BuildResult buildResult = new BuildResult();

        [FormerlySerializedAs("_rounds")]
        [SerializeField] uint rounds = 20;
        [FormerlySerializedAs("_filter")]
        [SerializeField] int filter = 1;
        [FormerlySerializedAs("_fallback")]
        [SerializeField] int fallback = 5;
        [FormerlySerializedAs("_algorithm")]
        [SerializeField] int algorithm = 1;
#pragma warning disable CS0414
        [FormerlySerializedAs("keySizeIdx")]
        [FormerlySerializedAs("_keySizeIndex")]
        [SerializeField] int keySizeIndex = 3;
#pragma warning restore CS0414
        [FormerlySerializedAs("_keySize")]
        [SerializeField] int keySize = 12;
        [FormerlySerializedAs("_syncSize")]
        [SerializeField] int syncSize = 1;
        [FormerlySerializedAs("_deleteFolders")]
        [SerializeField] bool deleteFolders = true;
        [FormerlySerializedAs("bUseSmallMipTexture")]
        [FormerlySerializedAs("_useSmallMipTexture")]
        [SerializeField] bool useSmallMipTexture = true;

        [FormerlySerializedAs("bPreserveMMD")]
        [FormerlySerializedAs("_preserveMmd")]
        [SerializeField] bool preserveMmd = true;

        [FormerlySerializedAs("_turnOnAllSafetyFallback")]
        [SerializeField] bool turnOnAllSafetyFallback = true;

        public static readonly string[] FilterStrings = new string[2] { "Point", "Bilinear" };
        public static readonly string[] FallbackStrings = new string[8] { "white", "black", "4x4", "8x8", "16x16", "32x32", "64x64", "128x128" };

        Texture2D fallbackWhite;
        Texture2D fallbackBlack;

        string GetPackageAssetDir()
        {
            if (!string.IsNullOrEmpty(packageAssetDir))
                return packageAssetDir;

            MonoScript monoScript = MonoScript.FromMonoBehaviour(this);
            string scriptPath = AssetDatabase.GetAssetPath(monoScript);
            packageAssetDir = OutputPaths.Normalize(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(scriptPath))));
            return packageAssetDir;
        }

        string GetRuntimeAssetDir()
        {
            return OutputPaths.Combine(GetPackageAssetDir(), "Runtime");
        }

        string ResolveOutputAssetDir()
        {
            string normalized = OutputPaths.Normalize(assetDir).TrimEnd('/');
            if (string.IsNullOrEmpty(normalized) || normalized == LegacyOutputDir || normalized == WrongRuntimeOutputDir || normalized == GetRuntimeAssetDir())
                normalized = DefaultOutputDir;

            assetDir = normalized;
            return assetDir;
        }

        OutputPaths GetOutputPaths()
        {
            if (outputPaths == null)
                outputPaths = new OutputPaths(assetDir, descriptor != null ? descriptor.gameObject : null);
            return outputPaths;
        }

        OutputPaths EnsureOutputFolders()
        {
            assetDir = ResolveOutputAssetDir();
            OutputPaths paths = GetOutputPaths();
            if (paths.Folders == null)
                paths.PrepareFolders(assetWriter, false);
            return paths;
        }

        public void Init()
        {
            if (descriptor == null)
                return;

            Transform body = descriptor.transform.Find("Body");
            if (body == null)
                return;

            if (!gameObjectList.Contains(body.gameObject))
                gameObjectList.Add(body.gameObject);

            SkinnedMeshRenderer renderer = body.GetComponent<SkinnedMeshRenderer>();
            if (renderer != null && renderer.sharedMesh != null && !obfuscationRenderers.Contains(renderer))
                obfuscationRenderers.Add(renderer);
        }

        public void SyncMatOption()
        {
            foreach (var pair in matOptionSaved)
            {
                if (pair.material != null)
                    materialOptions[pair.material] = pair.option;
            }
        }
        // Selected emission maps of the active materials, read from the saved options so the inspector needs no sync.
        public int CountEncryptedEmissionMaps(out int materialCount)
        {
            int maps = 0;
            materialCount = 0;
            foreach (var pair in matOptionSaved)
            {
                if (pair.material == null || pair.option == null || !pair.option.Active || pair.option.EmissionMask == 0)
                    continue;
                materialCount++;
                for (int mask = pair.option.EmissionMask; mask != 0; mask &= mask - 1)
                    maps++;
            }
            return maps;
        }
        public void SaveMatOption()
        {
            matOptionSaved.Clear();
            foreach (var pair in materialOptions)
            {
                matOptionSaved.Add(new MaterialOptionPair { material = pair.Key, option = pair.Value });
            }
        }

        UserKey userKey;
        string userKeyPassword;

        void Reset()
        {
            EnsureParameterSalt();
            if (descriptor == null)
                descriptor = GetComponentInParent<VRCAvatarDescriptor>(true);
            Init();
        }

        // Returns true if a new salt was generated.
        public bool EnsureParameterSalt()
        {
            if (UserKey.IsValidSalt(parameterSalt))
                return false;
            parameterSalt = UserKey.GenerateSalt();
            if (PrefabUtility.IsPartOfPrefabInstance(this))
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            EditorUtility.SetDirty(this);
            return true;
        }

        // PBKDF2 is slow on purpose, so the result is reused until the inputs change.
        public UserKey GetUserKey()
        {
            EnsureParameterSalt();
            if (userKey == null || userKeyPassword != userPassword || userKey.Salt != parameterSalt || userKey.Length != keySize)
            {
                userKey = UserKey.Derive(userPassword, parameterSalt, keySize);
                userKeyPassword = userPassword;
            }
            return userKey;
        }

        public byte[] GetKeyBytes()
        {
            return KeyGenerator.MakeKeyBytes(fixedPassword, GetUserKey().GetKeyBytes());
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
            return Encrypt(useSmallMipTexture, isModular);
        }

        public GameObject Encrypt(bool useSmallMip, bool isModular = true)
        {
            var request = new BuildRequest(this, descriptor, useSmallMip, isModular);
            var result = new Pipeline().Encrypt(request, CreateSettings());
            ApplyBuildResult(result);
            return result.avatar;
        }

        internal BuildResult CurrentBuildResult => buildResult;

        internal void ApplyBuildResult(BuildResult result)
        {
            buildResult = result ?? new BuildResult();
        }

        internal BuildSettings CreateSettings()
        {
            return new BuildSettings
            {
                AssetDir = assetDir,
                FixedPassword = fixedPassword,
                UserPassword = userPassword,
                ParameterSalt = parameterSalt,
                Language = language,
                LanguageIndex = languageIndex,
                Rounds = rounds,
                Filter = filter,
                Fallback = fallback,
                Algorithm = algorithm,
                KeySize = keySize,
                SyncSize = syncSize,
                DeleteFolders = deleteFolders,
                UseSmallMipTexture = useSmallMipTexture,
                PreserveMMD = preserveMmd,
                TurnOnAllSafetyFallback = turnOnAllSafetyFallback
            };
        }

        internal void ApplySettings(BuildSettings settings)
        {
            if (settings == null)
                return;

            assetDir = settings.AssetDir;
            outputPaths = null;
            fixedPassword = settings.FixedPassword;
            userPassword = settings.UserPassword;
            parameterSalt = settings.ParameterSalt;
            language = settings.Language;
            languageIndex = settings.LanguageIndex;
            rounds = settings.Rounds;
            filter = settings.Filter;
            fallback = settings.Fallback;
            algorithm = settings.Algorithm;
            keySize = settings.KeySize;
            syncSize = settings.SyncSize;
            deleteFolders = settings.DeleteFolders;
            useSmallMipTexture = settings.UseSmallMipTexture;
            preserveMmd = settings.PreserveMMD;
            turnOnAllSafetyFallback = settings.TurnOnAllSafetyFallback;
        }

        internal GameObject EncryptLegacy(bool useSmallMip, bool isModular = true)
        {
            buildResult.meshes.Clear();
            buildResult.encryptedMaterials.Clear();
            buildResult.processedTextures.Clear();
            buildResult.otherSecretsTextures.Clear();

            SyncMatOption();

            string resourceDir = GetRuntimeAssetDir();
            assetDir = ResolveOutputAssetDir();
            outputPaths = new OutputPaths(assetDir, descriptor != null ? descriptor.gameObject : null);
            string avatarDir = outputPaths.Avatar;
            buildResult.avatarDir = avatarDir;

            Debug.Log("AssetDir: " + assetDir);
            CleanOutdatedEncrypted();

            if (EnsureParameterSalt() && isModular)
                Debug.LogWarning("[ShellProtector] The parameter salt was generated during the build, so it changes on every upload. Select the ShellProtector component once to save a salt.");
            SaltRegistry.Register(parameterSalt);

            if (fallbackWhite == null)
                fallbackWhite = AssetDatabase.LoadAssetAtPath(OutputPaths.Combine(resourceDir, "white.png"), typeof(Texture2D)) as Texture2D;
            if (fallbackBlack == null)
                fallbackBlack = AssetDatabase.LoadAssetAtPath(OutputPaths.Combine(resourceDir, "black.png"), typeof(Texture2D)) as Texture2D;

            if (descriptor == null)
            {
                Debug.LogError("Can't find avatar descriptor!");
                return null;
            }

            descriptor.gameObject.SetActive(true);
            // The fixed key bytes are stored in the encrypted materials, so nobody has to remember them and each build picks new ones.
            fixedPassword = KeyGenerator.GenerateRandomString(16 - keySize);
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
            byte[] keyBytes = GetKeyBytes();
            buildResult.keyBytes = keyBytes;

            CreateFolders();

            ///////////////////Select crypto algorithm/////////////////////
            IEncryptor encryptor = new XXTEA();
            if (algorithm == (int)Algorithm.Xxtea)
            {
                XXTEA xxtea = new XXTEA();
                xxtea.Rounds = rounds;
                encryptor = xxtea;
            }
            else if (algorithm == (int)Algorithm.Chacha)
            {
                Chacha20 chacha = new Chacha20();
                byte[] hash1 = KeyGenerator.GetKeyHash(keyBytes, KeyGenerator.GenerateRandomString(chacha.Nonce.Length));
                Array.Copy(hash1, 0, chacha.Nonce, 0, chacha.Nonce.Length);
                encryptor = chacha;
            }
            ///////////////////////////////////////////////////////////////

            if (history == null)
            {
                history = AssetDatabase.LoadAssetAtPath(outputPaths.History(), typeof(EncryptedHistory)) as EncryptedHistory;
                if (history == null)
                {
                    history = EncryptedHistory.CreateCurrent();
                    assetWriter.CreateAssetInFolder(history, outputPaths.Folders.RootGuid, outputPaths.HistoryName());
                }
            }
            history.LoadData();

            int progress = 0;
            int maxprogress = materials.Count;

            var mips = new Dictionary<int, Texture2D>();
            foreach (var mat in materials)
            {
                if (mat == null)
                    continue;
                int materialFilter = filter;
#if UNITY_2022
                MatOption option = materialOptions.GetValueOrDefault(mat, null);
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
                injector = InjectorFactory.GetInjector(mat.shader);
                if (injector == null)
                {
                    Debug.LogError(mat.shader + " is a unsupported shader! supported type:lilToon, poiyomi");
                    continue;
                }
                if (!ConditionCheck(mat))
                    continue;

                if (shaderManager.IsPoiyomi(mat.shader))
                {
                    if (!shaderManager.IsLockPoiyomi(mat))
                    {
                        shaderManager.LockShader(mat);
                        Debug.LogFormat("Lock: {0} - {1}", mat.name, AssetDatabase.GetAssetPath(mat.shader));
                    }
                }

                Debug.LogFormat("{0} : Start encrypt...", mat.name);

                Texture2D mainTexture = (Texture2D)mat.mainTexture;
                ShaderSecrets secrets = SelectShaderSecrets(mat);
                Shader encryptedShader = shaderManager.IsLilToon(mat.shader) ? null : IsEncryptedBefore(mat.shader, secrets);
                injector.Init(descriptor.gameObject, mainTexture, keyBytes, keySize, materialFilter, resourceDir, encryptor, secrets);

                int mipRefSize = Math.Max(mat.mainTexture.width, mat.mainTexture.height);
                if (!mips.ContainsKey(mipRefSize))
                {
                    Texture2D mipRef = GenerateMipRefTexture(outputPaths.MipTextureName(mipRefSize), mipRefSize, useSmallMip);
                    if (mipRef != null)
                        mips.Add(mipRefSize, mipRef);
                }

                TextureSettings.SetRWEnableTexture(mainTexture);
                TextureSettings.SetCrunchCompression(mainTexture, false);
                TextureSettings.SetGenerateMipmap(mainTexture, true);

                string encryptedShaderFolderGuid = outputPaths.EnsureShaderFolder(assetWriter, mat);
                string encryptedShaderPath = assetWriter.ResolveFolderPath(encryptedShaderFolderGuid);

                var processedTextureResult = GenerateEncryptedTexture(outputPaths, mat, encryptor, keyBytes, secrets);
                if (!processedTextureResult.HasValue)
                    continue;
                ProcessedTexture processedTexture = processedTextureResult.Value;

                Texture2D encryptedTex1 = processedTexture.Encrypted.Texture1;
                Texture2D encryptedTex2 = processedTexture.Encrypted.Texture2;

                //////////////////////Inject shader///////////////////////
                AuxiliaryTextures otherTex = GetLimOutlineTextures(mat);
                if (!EmissionEncryption.SupportsEmission(encryptedShader))
                    encryptedShader = null;
                if (encryptedShader == null)
                {
                    try
                    {
                        encryptedShader = injector.Inject(
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
                        history.Save(mat.shader, secrets);
                    }
                    catch (UnityException e)
                    {
                        Debug.LogError(e.Message);
                        continue;
                    }
                }
                /////////////////////////////////////////////////////////
                Texture2D fallback = GenerateFallbackTexture(outputPaths.FallbackTextureName(mainTexture), option, mainTexture, ref processedTexture);
                if (fallback == null)
                    Debug.LogErrorFormat("Failed to generate fallback texture: {0}", mainTexture.name);

                int maxSize = Math.Max(mainTexture.width, mainTexture.height);
                Texture2D mipTex = mips[maxSize];
                if (mipTex == null)
                    Debug.LogWarningFormat("mip_{0} is not exsist", maxSize);

                GenerateEncryptedMaterial(outputPaths.EncryptedMaterialName(mat), mat, encryptedShader, fallback, mipTex, otherTex, processedTexture, keyBytes, encryptor, secrets);
            } // Material loop
            EditorUtility.ClearProgressBar();

            ///////////////////////parameter////////////////////
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            av3.expressionParameters = ParameterManager.AddKeyParameter(av3.expressionParameters, keySize, syncSize, GetUserKey());
            assetWriter.CreateAssetInFolder(av3.expressionParameters, outputPaths.Folders.AvatarGuid, outputPaths.ParametersName(av3.expressionParameters.name));
            ////////////////////////////////////////////////////
            if (!isModular)
            {
                ReplaceMaterials(avatar);
                RemoveDuplicatedTextures(avatar);

                descriptor.gameObject.SetActive(false);

                var newDesriptor = avatar.transform.GetComponentInChildren<ShellProtector>(true).gameObject;
                var tester = newDesriptor.AddComponent<ShellProtectorTester>();
                tester.Language = language;
                tester.LanguageIndex = languageIndex;
                tester.Protector = this;
                tester.UserKeyLength = keySize;
                Selection.activeObject = tester;

#if MODULAR
                var maMergeAnims = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                foreach (var maMergeAnim in maMergeAnims)
                {
                    AnimatorController newAnim = AnimatorManager.DuplicateAnimator(maMergeAnim.animator, outputPaths, assetWriter);
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
            AvatarProcessor.ReplaceMaterials(avatar, buildResult);
        }
        AuxiliaryTextures GetLimOutlineTextures(Material mat)
        {
            AuxiliaryTextures others = new AuxiliaryTextures();
            if (shaderManager.IsPoiyomi(mat.shader))
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
            else if (shaderManager.IsLilToon(mat.shader))
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
            Dictionary<Texture2D, ProcessedTexture> processedTextures = buildResult.processedTextures;

            foreach (var mat in buildResult.encryptedMaterials.Values)
            {
                AuxiliaryTextures otherTex = GetLimOutlineTextures(mat);

                foreach (var name in mat.GetTexturePropertyNames())
                {
                    if (EmissionEncryption.IsEmissionMap(mat, name)) continue;
                    if (mat.GetTexture(name) == null)
                        continue;
                    if (!(mat.GetTexture(name) is Texture2D))
                        continue;

                    if (processedTextures.ContainsKey((Texture2D)mat.GetTexture(name)))
                    {
                        Texture2D mainTexture = (Texture2D)mat.GetTexture(name);
                        Texture2D encrypted0 = processedTextures[(Texture2D)mat.GetTexture(name)].Encrypted.Texture1;

                        int idx = processedTextures[(Texture2D)mat.GetTexture(name)].FallbackOptions.IndexOf(processedTextures[(Texture2D)mat.GetTexture(name)].FallbackOptions.Max());
                        Texture2D bigFallbackTexture = processedTextures[(Texture2D)mat.GetTexture(name)].Fallbacks[idx];

                        if (otherTex.LimTexture != null)
                        {
                            string texName = "";
                            if (shaderManager.IsPoiyomi(mat.shader))
                                texName = "_RimTex";
                            else if (shaderManager.IsLilToon(mat.shader))
                                texName = "_RimColorTex";

                            if (mainTexture == otherTex.LimTexture)
                                mat.SetTexture(texName, encrypted0);
                            else if (processedTextures.ContainsKey(otherTex.LimTexture))
                                mat.SetTexture(texName, null);

                        }
                        if (otherTex.LimTexture2 != null) //only poiyomi
                        {
                            string texName = "";
                            if (shaderManager.IsPoiyomi(mat.shader))
                                texName = "_Rim2Tex";

                            if (mainTexture == otherTex.LimTexture2)
                                mat.SetTexture(texName, encrypted0);
                            else if (processedTextures.ContainsKey(otherTex.LimTexture2))
                                mat.SetTexture(texName, null);
                        }
                        if (otherTex.OutlineTexture != null)
                        {
                            string texName = "";
                            if (shaderManager.IsPoiyomi(mat.shader))
                                texName = "_OutlineTexture";
                            else if (shaderManager.IsLilToon(mat.shader))
                                texName = "_OutlineTex";

                            if (mainTexture == otherTex.OutlineTexture)
                                mat.SetTexture(texName, bigFallbackTexture);
                            else if (processedTextures.ContainsKey(otherTex.OutlineTexture))
                                mat.SetTexture(texName, processedTextures[otherTex.OutlineTexture].Fallbacks[0]);
                        }
                        if (otherTex.LimShadeTexture != null) //only liltoon
                        {
                            string texName = "_RimShadeMask";
                            if (mainTexture == otherTex.LimShadeTexture)
                                mat.SetTexture(texName, bigFallbackTexture);
                            else if (processedTextures.ContainsKey(otherTex.LimShadeTexture))
                                mat.SetTexture(texName, null);
                        }
                    }
                }
            } // Encrypted materials loop

            var duplicatedMaterials = new Dictionary<Material, Material>();
            bool changedFallbackMaterials = ReplaceProcessedTexturesWithFallbacks(avatar.GetComponentsInChildren<MeshRenderer>(true), paths, duplicatedMaterials);
            changedFallbackMaterials |= ReplaceProcessedTexturesWithFallbacks(avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true), paths, duplicatedMaterials);
            if (changedFallbackMaterials)
                assetWriter.SaveAndRefresh();
        }

        bool ReplaceProcessedTexturesWithFallbacks<T>(IEnumerable<T> renderers, OutputPaths paths, Dictionary<Material, Material> duplicatedMaterials) where T : Renderer
        {
            bool changed = false;
            Dictionary<Texture2D, ProcessedTexture> processedTextures = buildResult.processedTextures;

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
                        // Emission is opt-in, even if an unselected slot shares
                        // a texture encrypted as another material's main map.
                        if (EmissionEncryption.IsEmissionMap(sourceMaterial, name)) continue;
                        Texture2D texture = sourceMaterial.GetTexture(name) as Texture2D;
                        if (texture == null || !processedTextures.TryGetValue(texture, out ProcessedTexture processedTexture))
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

            string duplicatedPath = OutputPaths.Combine(assetWriter.ResolveFolderPath(paths.Folders.MatGuid), paths.DuplicatedMaterialName(source));
            duplicatedMaterial = AssetDatabase.LoadAssetAtPath<Material>(duplicatedPath);
            if (duplicatedMaterial == null)
            {
                duplicatedMaterial = Instantiate(source);
                assetWriter.CreateAssetInFolder(duplicatedMaterial, paths.Folders.MatGuid, paths.DuplicatedMaterialName(source));
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
            OutputPaths paths = EnsureOutputFolders();
            AnimatorController fx;
            if (clone)
            {
                var sourceControllers = new RuntimeAnimatorController[av3.baseAnimationLayers.Length];
                for (int i = 0; i < av3.baseAnimationLayers.Length; ++i)
                    sourceControllers[i] = av3.baseAnimationLayers[i].animatorController;

                AnimatorController[] duplicatedLayers = AnimatorManager.DuplicateAnimators(sourceControllers, paths, assetWriter);
                for (int i = 0; i < duplicatedLayers.Length; ++i)
                {
                    if (duplicatedLayers[i] != null)
                        av3.baseAnimationLayers[i].animatorController = duplicatedLayers[i];
                }

                fx = duplicatedLayers[4];
            }
            else
                fx = av3.baseAnimationLayers[4].animatorController as AnimatorController;

            string animationDir = assetWriter.ResolveFolderPath(paths.Folders.AnimGuid);

            GameObject[] meshArray = new GameObject[buildResult.meshes.Count];
            buildResult.meshes.CopyTo(meshArray);
            AnimatorManager.CreateKeyAnimations(OutputPaths.Combine(GetRuntimeAssetDir(), "Animations"), paths, assetWriter, meshArray);
            AnimatorManager.AddKeyLayer(fx, animationDir, keySize, syncSize, 3.0f, GetUserKey());

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
            foreach (var pair in buildResult.encryptedMaterials)
            {
                Debug.LogFormat("{0}, {1}", pair.Key.name, pair.Value.name);
                animManager.ChangeAnimationMaterial(fx, pair.Key, pair.Value, clone, paths, assetWriter);
            }

#if MODULAR
            if (clone)
            {
                var maMergeAnims = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                foreach (var maMergeAnim in maMergeAnims)
                {
                    if (maMergeAnim.animator == null)
                        continue;
                    foreach (var pair in buildResult.encryptedMaterials)
                    {
                        animManager.ChangeAnimationMaterial(maMergeAnim.animator as AnimatorController, pair.Key, pair.Value, clone, paths, assetWriter);
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

        public static AnimatorController GetFx(GameObject avatar, int playableLayer = 4)
        {
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (av3 == null)
                return null;
            return av3.baseAnimationLayers[playableLayer].animatorController as AnimatorController;
        }

        public void ObfuscateBlendShape(GameObject avatar, bool clone)
        {
            // Clone true = Manual encrypt
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            AnimatorController fx = GetFx(avatar);
            OutputPaths paths = EnsureOutputFolders();

            Obfuscator obfuscator = ScriptableObject.CreateInstance<Obfuscator>();
            obfuscator.Clone = clone;
            obfuscator.PreserveMmd = preserveMmd;

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
                Mesh newMesh = obfuscator.ObfuscateBlendShapeMesh(mesh, paths, assetWriter);
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
                        obfuscator.ObfuscateBlendshapeInAnim(maMergeAnim.animator as AnimatorController, selectRenderer.gameObject, paths, assetWriter);
                    }
                }
#endif
                for (int i = 0; i <= 4; ++i)
                {
                    AnimatorController playableLayer = GetFx(avatar, i);
                    if (playableLayer == null) 
                        continue;
                    obfuscator.ObfuscateBlendshapeInAnim(playableLayer, selectRenderer.gameObject, paths, assetWriter);
                }
                obfuscator.ChangeObfuscatedBlendShapeInDescriptor(av3, selectRenderer);
                obfuscator.Clean();
            }
        }

        public int GetEncryptedFoldersCount()
        {
            assetDir = ResolveOutputAssetDir();
            if (!Directory.Exists(assetDir))
            {
                return 0;
            }
            else
            {
                string[] directories = Directory.GetDirectories(assetDir);
                int deletedCount = 0;

                foreach (string dir in directories)
                {
                    if (IsGeneratedOutputFolder(dir))
                        deletedCount++;
                }
                return deletedCount;
            }
        }
        // Textures encrypted with an older format version decrypt to noise with the current shader,
        // so drop every previous output (and the history) before this build writes anything.
        void CleanOutdatedEncrypted()
        {
            var outdated = AssetDatabase.LoadAssetAtPath<EncryptedHistory>(OutputPaths.Combine(assetDir, "EncryptedHistory.asset"));
            if (outdated == null || !outdated.IsOutdatedFormat)
                return;

            Debug.LogWarning("[ShellProtector] Previously encrypted files use an older format and are being deleted.");
            CleanEncrypted();
            history = null;
        }

        public void CleanEncrypted()
        {
            assetDir = ResolveOutputAssetDir();
            AssetDatabase.DeleteAsset(OutputPaths.Combine(assetDir, "EncryptedHistory.asset"));

            if (!Directory.Exists(assetDir))
            {
                Debug.LogError($"The specified path does not exist: {assetDir}");
            }
            else
            {
                string[] directories = Directory.GetDirectories(assetDir);
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
            return filter;
        }
        public int GetDefaultFallback()
        {
            return fallback;
        }
        public int GetKeySize()
        {
            return keySize;
        }

        public void ResetMaterialOptions()
        {
            matOptionSaved.Clear();
            materialOptions.Clear();
        }

        // A Poiyomi copy bakes the secrets of its main texture, so materials sharing a texture share one encryption of it.
        // lilToon materials all use the project's shader (LilToonShaders).
        ShaderSecrets SelectShaderSecrets(Material mat)
        {
            if (shaderManager.IsLilToon(mat.shader))
                return LilToonShaders.GetSecrets();

            return history.GetTextureSecrets((Texture2D)mat.mainTexture);
        }

        public Shader IsEncryptedBefore(Shader shader, ShaderSecrets secrets = null)
        {
            if (history == null)
            {
                history = AssetDatabase.LoadAssetAtPath(GetOutputPaths().History(), typeof(EncryptedHistory)) as EncryptedHistory;
                if (history == null)
                {
                    history = EncryptedHistory.CreateCurrent();
                    OutputPaths paths = GetOutputPaths();
                    if (paths.Folders == null)
                        paths.PrepareFolders(assetWriter, false);
                    assetWriter.CreateAssetInFolder(history, paths.Folders.RootGuid, paths.HistoryName());
                }
            }
            history.LoadData();
            return history.IsEncryptedBefore(shader, secrets);
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
        public List<Material> GetMaterials()
        {
            List<Material> materials = new List<Material>();
            foreach (GameObject g in gameObjectList)
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

            return materials.Concat(materialList).Distinct().ToList();
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
            paths.PrepareFolders(assetWriter, deleteFolders && AssetDatabase.IsValidFolder(paths.Avatar));
        }

        Texture2D GenerateMipRefTexture(string fileName, int size, bool useSmallMip)
        {
            var mip = TextureEncryptManager.GenerateRefMipmap(size, size, useSmallMip);
            if (mip == null)
                Debug.LogErrorFormat("{0} : Can't generate mip tex{1}.", fileName, size);
            else
            {
                assetWriter.CreateAssetInFolder(mip, GetOutputPaths().Folders.TexGuid, fileName);
                assetWriter.SaveAndRefresh();
            }
            return mip;
        }
        ProcessedTexture? GenerateEncryptedTexture(OutputPaths paths, Material mat, IEncryptor encryptor, byte[] keyBytes, ShaderSecrets secrets)
        {
            Texture2D mainTexture = (Texture2D)mat.mainTexture;

            string texName1 = paths.EncryptedTextureName(mainTexture, 0);
            string texName2 = paths.EncryptedTextureName(mainTexture, 2);

            bool processed = buildResult.processedTextures.ContainsKey(mainTexture);
            ProcessedTexture processedTexture;
            if (processed)
                processedTexture = buildResult.processedTextures[mainTexture];
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
            if (algorithm == (int)Algorithm.Chacha)
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
                    byte[] nonce = buildResult.processedTextures[mainTexture].Nonce;
                    Array.Copy(nonce, 0, chacha.Nonce, 0, chacha.Nonce.Length);
                }
            }

            if (processed && !secrets.Matches(processedTexture.Secrets))
                return EncryptForOtherSecrets(paths, mainTexture, processedTexture, encryptor, keyBytes, secrets);

            if (!processed)
            {
                EncryptResult encryptResult;
                try
                {
                    encryptResult = TextureEncryptManager.EncryptTexture(mainTexture, keyBytes, encryptor, secrets);
                }
                catch (ArgumentException e)
                {
                    Debug.LogErrorFormat("{0} : ArgumentException - {1}", mainTexture.name, e.Message);
                    return null;
                }
                assetWriter.CreateAssetInFolder(encryptResult.Texture1, paths.Folders.TexGuid, texName1);
                if (encryptResult.Texture2 != null)
                    assetWriter.CreateAssetInFolder(encryptResult.Texture2, paths.Folders.TexGuid, texName2);

                processedTexture.Encrypted = encryptResult;
                processedTexture.Secrets = secrets;

                buildResult.processedTextures.Add(mainTexture, processedTexture);
            }

            return processedTexture;
        }

        // The nonce and fallbacks stay those of the processed texture; only the encrypted textures differ.
        ProcessedTexture? EncryptForOtherSecrets(OutputPaths paths, Texture2D mainTexture, ProcessedTexture processedTexture, IEncryptor encryptor, byte[] keyBytes, ShaderSecrets secrets)
        {
            var key = (mainTexture, secrets.ToDefines());
            if (!buildResult.otherSecretsTextures.TryGetValue(key, out EncryptResult encryptResult))
            {
                try
                {
                    encryptResult = TextureEncryptManager.EncryptTexture(mainTexture, keyBytes, encryptor, secrets);
                }
                catch (ArgumentException e)
                {
                    Debug.LogErrorFormat("{0} : ArgumentException - {1}", mainTexture.name, e.Message);
                    return null;
                }

                int variant = buildResult.otherSecretsTextures.Keys.Count(k => k.Item1 == mainTexture) + 1;
                assetWriter.CreateAssetInFolder(encryptResult.Texture1, paths.Folders.TexGuid, paths.EncryptedTextureName(mainTexture, 0, variant));
                if (encryptResult.Texture2 != null)
                    assetWriter.CreateAssetInFolder(encryptResult.Texture2, paths.Folders.TexGuid, paths.EncryptedTextureName(mainTexture, 2, variant));
                buildResult.otherSecretsTextures.Add(key, encryptResult);
            }

            processedTexture.Encrypted = encryptResult;
            processedTexture.Secrets = secrets;
            return processedTexture;
        }
        Texture2D GenerateFallbackTexture(string fileName, MatOption option, Texture2D mainTexture, ref ProcessedTexture processedTexture)
        {
            int fallbackOption = this.fallback;
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
                        assetWriter.CreateAssetInFolder(fallback, GetOutputPaths().Folders.TexGuid, fileName);
                        assetWriter.SaveAndRefresh();
                    }
                }
                else
                {
                    switch (fallbackSize)
                    {
                        case 0:
                            processedTexture.Fallbacks.Add(fallbackWhite);
                            processedTexture.FallbackOptions.Add(fallbackOption);
                            fallback = fallbackWhite;
                            break;
                        case 1:
                            processedTexture.Fallbacks.Add(fallbackBlack);
                            processedTexture.FallbackOptions.Add(fallbackOption);
                            fallback = fallbackBlack;
                            break;
                    }
                }
            }
            else
                fallback = processedTexture.Fallbacks[idx];

            return fallback;
        }
        Material GenerateEncryptedMaterial(string fileName, Material mat, Shader encryptedShader, Texture2D fallback, Texture2D mip, AuxiliaryTextures otherTex, ProcessedTexture processedTexture, byte[] keyBytes, IEncryptor encryptor, ShaderSecrets secrets)
        {
            MaterialEncryptor materialEncryptor = new MaterialEncryptor(assetWriter, turnOnAllSafetyFallback, algorithm, rounds);
            int emissionMask = materialOptions.TryGetValue(mat, out var option) ? option.EmissionMask : 0;
            Material newMat = materialEncryptor.CreateEncryptedMaterial(GetOutputPaths().Folders.MatGuid, fileName, mat, encryptedShader, fallback, mip, otherTex, processedTexture, keyBytes, 16 - keySize, encryptor, injector, secrets, emissionMask);
            Debug.LogFormat("{0} : create encrypted material : {1}", mat.name, AssetDatabase.GetAssetPath(newMat));

            if (!buildResult.encryptedMaterials.ContainsKey(mat))
                buildResult.encryptedMaterials.Add(mat, newMat);

            return newMat;
        }
    }
}
#endif
