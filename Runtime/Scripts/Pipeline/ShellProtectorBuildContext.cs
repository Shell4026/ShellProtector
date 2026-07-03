#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
    public struct ShellProtectorProcessedTexture
    {
        public EncryptResult encrypted;
        public List<Texture2D> fallbacks;
        public List<int> fallbackOptions;
        public byte[] nonce;
    }

    public struct ShellProtectorAuxiliaryTextures
    {
        public Texture2D limTexture;
        public Texture2D limTexture2;
        public Texture2D outlineTexture;
        public Texture2D limShadeTexture;
    }

    public sealed class ShellProtectorBuildContext
    {
        public ShellProtectorBuildContext(ShellProtectorBuildRequest request, ShellProtectorSettings settings)
        {
            Request = request;
            Settings = settings;
            Result = new ShellProtectorBuildResult();
        }

        public ShellProtectorBuildRequest Request { get; }
        public ShellProtectorSettings Settings { get; }
        public ShellProtectorBuildResult Result { get; }
        public IEncryptor Encryptor { get; set; }
        public EncryptedHistory History { get; set; }
        public Texture2D FallbackWhite { get; set; }
        public Texture2D FallbackBlack { get; set; }
    }
}
#endif
