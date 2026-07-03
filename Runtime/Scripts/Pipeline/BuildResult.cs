#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
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
