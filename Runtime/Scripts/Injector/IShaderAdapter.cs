#if UNITY_EDITOR
using UnityEngine;

namespace Shell.Protector
{
    public interface IShaderAdapter
    {
        bool CanHandle(Shader shader);
        bool WasInjected(Shader shader);
        void SetKeywords(Material material, bool hasLimTexture = false);
        Shader Inject(Material material, string decoderPath, string outputPath, Texture2D mainTexture, AuxiliaryTextures auxiliaryTextures);
    }
}
#endif
