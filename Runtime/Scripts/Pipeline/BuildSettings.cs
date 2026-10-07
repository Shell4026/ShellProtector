#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
    public sealed class BuildSettings
    {
        public string AssetDir { get; set; }
        // The package's Runtime folder: shaders, key animations and fallback textures.
        public string RuntimeDir { get; set; }
        public UserKey UserKey { get; set; }
        public string Language { get; set; }
        public int LanguageIndex { get; set; }
        public uint Rounds { get; set; }
        public int Filter { get; set; }
        public int Fallback { get; set; }
        public int Algorithm { get; set; }
        public int KeySize { get; set; }
        public int SyncSize { get; set; }
        public bool DeleteFolders { get; set; }
        public bool UseSmallMipTexture { get; set; }
        public bool PreserveMMD { get; set; }
        public bool TurnOnAllSafetyFallback { get; set; }
        public List<Material> Materials { get; set; } = new List<Material>();
        public Dictionary<Material, ShellProtector.MatOption> MaterialOptions { get; set; } = new Dictionary<Material, ShellProtector.MatOption>();
        public List<SkinnedMeshRenderer> ObfuscationRenderers { get; set; } = new List<SkinnedMeshRenderer>();
    }
}
#endif
