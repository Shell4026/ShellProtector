#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Debug = UnityEngine.Debug;

namespace Shell.Protector
{
    // One encryption of an avatar. Run encrypts the materials (Pipeline.Materials.cs), adds the key parameters, and then
    // rewrites the rest of the avatar (Pipeline.Avatar.cs). Assets are saved once at the end: everything is created
    // through the AssetDatabase, which imports it right away, so nothing in between needs a save or a refresh.
    public sealed partial class Pipeline
    {
        readonly BuildRequest request;
        readonly BuildSettings settings;
        readonly AssetWriter writer = new AssetWriter();
        readonly AssetManager shaderManager = AssetManager.GetInstance();
        readonly BuildTimings timings = new BuildTimings();

        OutputPaths paths;
        EncryptedHistory history;
        IEncryptor encryptor;
        Texture2D fallbackWhite;
        Texture2D fallbackBlack;

        public Pipeline(BuildRequest request, BuildSettings settings)
        {
            this.request = request;
            this.settings = settings;
        }

        public BuildResult Result { get; } = new BuildResult();

        // The avatar being rewritten: the copy when cloning, otherwise the requested avatar.
        public GameObject Avatar { get; private set; }

        public BuildResult Run()
        {
            if (request?.Avatar == null)
            {
                Debug.LogError("Can't find avatar descriptor!");
                return Result;
            }

            using (timings.Measure("prepare"))
            {
                if (!Prepare())
                    return Result;
            }

            EncryptMaterials();

            using (timings.Measure("parameters"))
                AddKeyParameters();

            ProcessAvatar();

            using (timings.Measure("save"))
                AssetDatabase.SaveAssets();

            Result.avatar = Avatar;
            Debug.LogFormat("[ShellProtector] Build {0}: {1} materials, {2}", request.Avatar.name, Result.encryptedMaterials.Count, timings);
            return Result;
        }

        bool Prepare()
        {
            GameObject source = request.Avatar;
            var descriptor = source.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                Debug.LogError(source.name + ": can't find VRCAvatarDescriptor!");
                return false;
            }
            if (descriptor.expressionParameters == null)
            {
                Debug.LogError(source.name + ": can't find expressionParmeters!");
                return false;
            }

            Debug.Log("AssetDir: " + settings.AssetDir);
            CleanOutdatedOutputs();
            SaltRegistry.Register(settings.UserKey.Salt);

            fallbackWhite = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPaths.Combine(settings.RuntimeDir, "white.png"));
            fallbackBlack = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPaths.Combine(settings.RuntimeDir, "black.png"));

            source.SetActive(true);
            Avatar = source;
            if (request.Clone)
            {
                Avatar = DuplicateAvatar(source);
                Debug.Log("Duplicate avatar success.");
            }

            // The fixed key bytes are stored in the encrypted materials, so nobody has to remember them and each build picks new ones.
            Result.keyBytes = KeyGenerator.MakeKeyBytes(KeyGenerator.GenerateRandomString(16 - settings.KeySize), settings.UserKey.GetKeyBytes());
            Debug.Log("Key bytes: " + string.Join(", ", Result.keyBytes));

            paths = new OutputPaths(settings.AssetDir, source);
            paths.PrepareFolders(writer, settings.DeleteFolders && AssetDatabase.IsValidFolder(paths.Avatar));
            encryptor = CreateEncryptor(Result.keyBytes);
            history = LoadOrCreateHistory();
            return true;
        }

        // The avatar steps also run on their own (as tests do), without Prepare.
        void EnsureOutputs()
        {
            if (Avatar == null)
                Avatar = request.Avatar;
            if (paths != null)
                return;
            paths = new OutputPaths(settings.AssetDir, request.Avatar);
            paths.PrepareFolders(writer, false);
        }

        // Textures encrypted with an older format version decrypt to noise with the current shader,
        // so drop every previous output (and the history) before this build writes anything.
        void CleanOutdatedOutputs()
        {
            var outdated = AssetDatabase.LoadAssetAtPath<EncryptedHistory>(OutputPaths.HistoryPath(settings.AssetDir));
            if (outdated == null || !outdated.IsOutdatedFormat)
                return;

            Debug.LogWarning("[ShellProtector] Previously encrypted files use an older format and are being deleted.");
            OutputPaths.DeleteGenerated(settings.AssetDir);
        }

        static GameObject DuplicateAvatar(GameObject avatar)
        {
            GameObject copy = UnityEngine.Object.Instantiate(avatar);
            if (!avatar.name.Contains("_encrypted"))
                copy.name = avatar.name + "_encrypted";
            return copy;
        }

        IEncryptor CreateEncryptor(byte[] keyBytes)
        {
            if (settings.Algorithm == (int)ShellProtectorAlgorithm.Chacha)
            {
                Chacha20 chacha = new Chacha20();
                byte[] hash = KeyGenerator.GetKeyHash(keyBytes, KeyGenerator.GenerateRandomString(chacha.Nonce.Length));
                Array.Copy(hash, 0, chacha.Nonce, 0, chacha.Nonce.Length);
                return chacha;
            }

            XXTEA xxtea = new XXTEA();
            if (settings.Algorithm == (int)ShellProtectorAlgorithm.XXTEA)
                xxtea.Rounds = settings.Rounds;
            return xxtea;
        }

        EncryptedHistory LoadOrCreateHistory()
        {
            var loaded = AssetDatabase.LoadAssetAtPath<EncryptedHistory>(paths.History());
            if (loaded == null)
            {
                loaded = EncryptedHistory.CreateCurrent();
                writer.CreateAssetInFolder(loaded, paths.Folders.RootGuid, paths.HistoryName());
            }
            loaded.LoadData();
            return loaded;
        }

        void AddKeyParameters()
        {
            var descriptor = Avatar.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = ParameterManager.AddKeyParameter(descriptor.expressionParameters, settings.KeySize, settings.SyncSize, settings.UserKey);
            writer.CreateAssetInFolder(descriptor.expressionParameters, paths.Folders.AvatarGuid, paths.ParametersName(descriptor.expressionParameters.name));
        }

        // Seconds per stage, for the build log. A stage measured more than once adds up.
        sealed class BuildTimings
        {
            readonly Stopwatch total = Stopwatch.StartNew();
            readonly List<string> stages = new List<string>();
            readonly Dictionary<string, TimeSpan> elapsed = new Dictionary<string, TimeSpan>();

            public Scope Measure(string stage)
            {
                if (!elapsed.ContainsKey(stage))
                {
                    stages.Add(stage);
                    elapsed.Add(stage, TimeSpan.Zero);
                }
                return new Scope(this, stage);
            }

            public override string ToString()
            {
                return string.Format("{0:F1} s ({1})", total.Elapsed.TotalSeconds, string.Join(", ", stages.Select(s => string.Format("{0} {1:F1}", s, elapsed[s].TotalSeconds))));
            }

            public readonly struct Scope : IDisposable
            {
                readonly BuildTimings owner;
                readonly string stage;
                readonly long start;

                public Scope(BuildTimings owner, string stage)
                {
                    this.owner = owner;
                    this.stage = stage;
                    start = Stopwatch.GetTimestamp();
                }

                public void Dispose()
                {
                    owner.elapsed[stage] += TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency);
                }
            }
        }
    }
}
#endif
