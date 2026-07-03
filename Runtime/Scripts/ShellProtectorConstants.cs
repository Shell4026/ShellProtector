namespace Shell.Protector
{
    public enum ShellProtectorAlgorithm
    {
        XXTEA = 0,
        Chacha = 1
    }

    public enum ShellProtectorTextureFilter
    {
        Point = 0,
        Bilinear = 1
    }

    public enum ShellProtectorFallback
    {
        White = 0,
        Black = 1,
        Size4 = 2,
        Size8 = 3,
        Size16 = 4,
        Size32 = 5,
        Size64 = 6,
        Size128 = 7
    }

    public static class ShellProtectorShaderProperties
    {
        public const string KeywordPrefix = "_SHELL_PROTECTOR_";
        public const string XXTEAKeyword = "_SHELL_PROTECTOR_XXTEA";
        public const string ChachaKeyword = "_SHELL_PROTECTOR_CHACHA";
        public const string Format0Keyword = "_SHELL_PROTECTOR_FORMAT0";
        public const string Format1Keyword = "_SHELL_PROTECTOR_FORMAT1";
        public const string RimLightKeyword = "_SHELL_PROTECTOR_RIMLIGHT";

        public const string MipTexture = "_MipTex";
        public const string EncryptTexture0 = "_EncryptTex0";
        public const string EncryptTexture1 = "_EncryptTex1";
        public const string WidthOffset = "_Woffset";
        public const string HeightOffset = "_Hoffset";
        public const string Nonce0 = "_Nonce0";
        public const string Nonce1 = "_Nonce1";
        public const string Nonce2 = "_Nonce2";
        public const string Rounds = "_Rounds";
        public const string PasswordHash = "_PasswordHash";
        public const string HashMagic = "_HashMagic";
        public const string KeyPrefix = "_Key";
    }
}
