#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
    public struct ProcessedTexture
    {
        public EncryptResult Encrypted;
        public List<Texture2D> Fallbacks;
        public List<int> FallbackOptions;
        public byte[] Nonce;
        // What Encrypted was encrypted with (see ShaderSecrets).
        public ShaderSecrets Secrets;
    }

    public struct AuxiliaryTextures
    {
        public Texture2D LimTexture;
        public Texture2D LimTexture2;
        public Texture2D OutlineTexture;
        public Texture2D LimShadeTexture;
    }

    public sealed class BuildResult
    {
        // The encrypted avatar, or null if the build failed.
        public GameObject avatar { get; set; }
        // The 16 key bytes of this build: new fixed bytes, then the user key.
        public byte[] keyBytes { get; set; }
        public HashSet<GameObject> meshes { get; } = new HashSet<GameObject>();
        public Dictionary<Material, Material> encryptedMaterials { get; } = new Dictionary<Material, Material>();
        public Dictionary<Texture2D, ProcessedTexture> processedTextures { get; } = new Dictionary<Texture2D, ProcessedTexture>();
        // Extra encryptions of a processed texture for materials whose shaders bake other secrets, such as a lilToon and
        // a Poiyomi material sharing a main texture. Keyed by the texture and ShaderSecrets.ToDefines.
        public Dictionary<(Texture2D, string), EncryptResult> otherSecretsTextures { get; } = new Dictionary<(Texture2D, string), EncryptResult>();
    }
}
#endif
