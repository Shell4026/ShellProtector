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
        // What Encrypted was encrypted with (see ShaderSecrets).
        public ShaderSecrets Secrets;
    }

    public struct AuxiliaryTextures
    {
        public Texture2D LimTexture;
        public Texture2D LimTexture2;
        public Texture2D OutlineTexture;
        public Texture2D LimShadeTexture;

        // The rim and outline textures the injected shader samples through the decrypted main color. A rim texture turns
        // on the rim light override (Injector.SetKeywords), whether or not the material uses its rim light.
        public static AuxiliaryTextures Of(Material mat)
        {
            AssetManager shaderManager = AssetManager.GetInstance();
            AuxiliaryTextures others = new AuxiliaryTextures();
            if (shaderManager.IsPoiyomi(mat.shader))
            {
                foreach (var t in mat.GetTexturePropertyNames())
                {
                    if (t == "_RimTex")
                        others.LimTexture = (Texture2D)mat.GetTexture(t);
                    else if (t == "_Rim2Tex")
                        others.LimTexture2 = (Texture2D)mat.GetTexture(t);
                    else if (t == "_OutlineTexture")
                        others.OutlineTexture = (Texture2D)mat.GetTexture(t);
                }
            }
            else if (shaderManager.IsLilToon(mat.shader))
            {
                foreach (var t in mat.GetTexturePropertyNames())
                {
                    if (t == "_RimColorTex")
                        others.LimTexture = (Texture2D)mat.GetTexture(t);
                    else if (t == "_OutlineTex")
                        others.OutlineTexture = (Texture2D)mat.GetTexture(t);
                    else if (t == "_RimShadeMask")
                        others.LimShadeTexture = (Texture2D)mat.GetTexture(t);
                }
            }
            return others;
        }
    }

    public sealed class BuildResult
    {
        // The encrypted avatar, or null if the build failed.
        public GameObject avatar { get; set; }
        // The 16 key bytes of this build: new fixed bytes, then the user key.
        public byte[] keyBytes { get; set; }
        public HashSet<GameObject> meshes { get; } = new HashSet<GameObject>();
        public Dictionary<Material, Material> encryptedMaterials { get; } = new Dictionary<Material, Material>();
        public Dictionary<Texture2D, ProcessedTexture> processedTextures { get; } = new Dictionary<Texture2D, ProcessedTexture>();
        // Extra encryptions of a processed texture for materials whose shaders bake other secrets, such as a lilToon and
        // a Poiyomi material sharing a main texture. Keyed by the texture and ShaderSecrets.ToDefines.
        public Dictionary<(Texture2D, string), EncryptResult> otherSecretsTextures { get; } = new Dictionary<(Texture2D, string), EncryptResult>();
        // Each material that uses a protected texture and its copy with the fallbacks, for renderers and material swaps.
        // A material that uses none maps to null.
        public Dictionary<Material, Material> fallbackMaterials { get; } = new Dictionary<Material, Material>();
        public ClipCopies clipCopies { get; } = new ClipCopies();
    }
}
#endif
