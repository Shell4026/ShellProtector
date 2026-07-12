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
        public GameObject Avatar { get; set; }
        public string AvatarDir { get; set; }
        public byte[] KeyBytes { get; set; }
        public HashSet<GameObject> Meshes { get; } = new HashSet<GameObject>();
        public Dictionary<Material, Material> EncryptedMaterials { get; } = new Dictionary<Material, Material>();
        public Dictionary<Texture2D, ProcessedTexture> ProcessedTextures { get; } = new Dictionary<Texture2D, ProcessedTexture>();

        public void Clear()
        {
            Avatar = null;
            AvatarDir = null;
            KeyBytes = null;
            Meshes.Clear();
            EncryptedMaterials.Clear();
            ProcessedTextures.Clear();
        }
    }
}
#endif
