#if UNITY_EDITOR

using UnityEngine;

namespace Shell.Protector
{
    // Problems that make the build skip a material or one of its emission maps. The material settings window shows them
    // per material and the inspector sums them up, so both check the same conditions.
    internal static class MaterialIssues
    {
        public enum Issue
        {
            None,
            UnsupportedShader,
            EmptyMainTexture,
            MainTextureNotTexture2D,
            UnsupportedMainTextureFormat,
            OddMainTextureSize
        }

        static readonly AssetManager assetManager = AssetManager.GetInstance();

        // The first problem with the main texture, as the build checks it (Pipeline.EncryptMaterial).
        public static Issue Check(Material material)
        {
            if (!assetManager.IsSupportShader(material.shader))
                return Issue.UnsupportedShader;
            if (material.mainTexture == null)
                return Issue.EmptyMainTexture;
            if (!(material.mainTexture is Texture2D mainTexture))
                return Issue.MainTextureNotTexture2D;
            if (!TextureEncryptManager.IsSupportedTexture(mainTexture))
                return Issue.UnsupportedMainTextureFormat;
            if (mainTexture.width % 2 != 0 || mainTexture.height % 2 != 0)
                return Issue.OddMainTextureSize;
            return Issue.None;
        }

        public static string Message(Issue issue)
        {
            switch (issue)
            {
                case Issue.UnsupportedShader: return "Not supported shader";
                case Issue.EmptyMainTexture: return "The main texture is empty.";
                case Issue.MainTextureNotTexture2D: return "The main texture is not Texture2D.";
                case Issue.UnsupportedMainTextureFormat: return "The main texture is not supported format.";
                case Issue.OddMainTextureSize: return "The main texture size must be a multiple of 2.";
                default: return "";
            }
        }

        // The emission maps of the shader, indexed by EmissionMask slot.
        public static string[] EmissionMaps(Material material)
        {
            return assetManager.IsPoiyomi(material.shader) ? EmissionEncryption.PoiyomiMaps : EmissionEncryption.LilToonMaps;
        }

        // Whether a selected slot holds a texture the build can't encrypt.
        public static bool HasUnsupportedEmission(Material material, int mask)
        {
            if (mask == 0 || !assetManager.IsSupportShader(material.shader))
                return false;
            string[] maps = EmissionMaps(material);
            for (int slot = 0; slot < maps.Length; slot++)
            {
                if ((mask & (1 << slot)) != 0 && !IsSupportedEmission(material, slot))
                    return true;
            }
            return false;
        }

        // An empty slot has nothing to encrypt. A slot that reuses the main texture's decryption needs no encryption of its
        // own, so it takes any main texture format.
        public static bool IsSupportedEmission(Material material, int slot)
        {
            string map = EmissionMaps(material)[slot];
            if (!material.HasProperty(map) || material.GetTexture(map) == null)
                return true;
            return IsSupportedEmission(material.GetTexture(map)) || EmissionEncryption.ReusesMainTexture(material, slot);
        }

        // The conditions EmissionEncryption.Encrypt checks at build time, except readability, which the build sets itself.
        public static bool IsSupportedEmission(Texture texture)
        {
            if (!(texture is Texture2D texture2D) || !EmissionEncryption.SupportsFormat(texture2D))
                return false;
            if (!Mathf.IsPowerOfTwo(texture2D.width) || !Mathf.IsPowerOfTwo(texture2D.height) || texture2D.width * texture2D.height < 2)
                return false;
            bool dxt = TextureEncryptManager.IsDXTFormat(texture2D.format) || texture2D.format == TextureFormat.DXT1Crunched || texture2D.format == TextureFormat.DXT5Crunched;
            return !dxt || (texture2D.width >= 8 && texture2D.height >= 4);
        }
    }
}
#endif
