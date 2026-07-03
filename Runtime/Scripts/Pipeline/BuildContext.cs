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

    public sealed class BuildContext
    {
        public BuildContext(BuildRequest request, BuildSettings settings)
        {
            Request = request;
            Settings = settings;
            Result = new BuildResult();
        }

        public BuildRequest Request { get; }
        public BuildSettings Settings { get; }
        public BuildResult Result { get; }
        public IEncryptor Encryptor { get; set; }
        public EncryptedHistory History { get; set; }
        public Texture2D FallbackWhite { get; set; }
        public Texture2D FallbackBlack { get; set; }
    }
}
#endif
