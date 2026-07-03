#if UNITY_EDITOR
namespace Shell.Protector
{
    public sealed class BuildSettings
    {
        public string AssetDir { get; set; }
        public string FixedPassword { get; set; }
        public string UserPassword { get; set; }
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
    }
}
#endif
