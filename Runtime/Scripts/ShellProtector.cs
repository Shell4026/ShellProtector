#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Serialization;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace Shell.Protector
{
    // The settings of an avatar's encryption. The build itself is Pipeline.
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

        string packageAssetDir;

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

        string ResolveOutputAssetDir()
        {
            string normalized = OutputPaths.Normalize(assetDir).TrimEnd('/');
            if (string.IsNullOrEmpty(normalized) || normalized == LegacyOutputDir || normalized == WrongRuntimeOutputDir || normalized == GetRuntimeAssetDir())
                normalized = DefaultOutputDir;

            assetDir = normalized;
            return assetDir;
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
        public GameObject Encrypt(bool isModular = true)
        {
            return Encrypt(useSmallMipTexture, isModular);
        }

        // isModular encrypts the avatar in place, as the NDMF pass on upload does. Otherwise a copy is encrypted and the original is disabled.
        public GameObject Encrypt(bool useSmallMip, bool isModular = true)
        {
            if (descriptor == null)
            {
                Debug.LogError("Can't find avatar descriptor!");
                return null;
            }

            if (EnsureParameterSalt() && isModular)
                Debug.LogWarning("[ShellProtector] The parameter salt was generated during the build, so it changes on every upload. Select the ShellProtector component once to save a salt.");

            BuildSettings settings = CreateSettings();
            settings.UseSmallMipTexture = useSmallMip;
            BuildResult result = new Pipeline(new BuildRequest(descriptor.gameObject, !isModular, this), settings).Run();

            if (!isModular && result.avatar != null)
                Selection.activeObject = result.avatar.GetComponentInChildren<ShellProtectorTester>(true);
            return result.avatar;
        }

        public BuildSettings CreateSettings()
        {
            SyncMatOption();
            return new BuildSettings
            {
                AssetDir = ResolveOutputAssetDir(),
                RuntimeDir = GetRuntimeAssetDir(),
                UserKey = GetUserKey(),
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
                TurnOnAllSafetyFallback = turnOnAllSafetyFallback,
                Materials = GetMaterials(),
                MaterialOptions = new Dictionary<Material, MatOption>(materialOptions),
                ObfuscationRenderers = new List<SkinnedMeshRenderer>(obfuscationRenderers)
            };
        }
        public void CleanEncrypted()
        {
            OutputPaths.DeleteGenerated(ResolveOutputAssetDir());
        }

        public void ResetMaterialOptions()
        {
            matOptionSaved.Clear();
            materialOptions.Clear();
        }

        // The injected copy of the shader that the next build reuses, if any.
        public Shader IsEncryptedBefore(Shader shader, ShaderSecrets secrets = null)
        {
            var history = AssetDatabase.LoadAssetAtPath<EncryptedHistory>(OutputPaths.HistoryPath(ResolveOutputAssetDir()));
            if (history == null)
                return null;
            history.LoadData();
            return history.IsEncryptedBefore(shader, secrets);
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
        public VRCExpressionParameters GetParameter()
        {
            var av3 = descriptor;
            if (av3 == null)
                return null;
            return av3.expressionParameters;
        }
        public int GetEncryptedFoldersCount()
        {
            return OutputPaths.CountGeneratedFolders(ResolveOutputAssetDir());
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
        public static int GetRequiredSwitchCount(int keyLength, int syncSize)
        {
            keyLength /= syncSize;
            return Mathf.CeilToInt(Mathf.Log(keyLength, 2));
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
    }
}
#endif
