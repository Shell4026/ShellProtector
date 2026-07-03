#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
    public struct ShellProtectorProcessedTexture
    {
        public EncryptResult Encrypted;
        public List<Texture2D> Fallbacks;
        public List<int> FallbackOptions;
        public byte[] Nonce;
    }

    public struct ShellProtectorAuxiliaryTextures
    {
        public Texture2D LimTexture;
        public Texture2D LimTexture2;
        public Texture2D OutlineTexture;
        public Texture2D LimShadeTexture;
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
