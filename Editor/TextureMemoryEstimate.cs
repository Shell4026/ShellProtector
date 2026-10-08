#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Shell.Protector
{
    // The change in the avatar's texture memory (VRAM) that the next build makes. It mirrors what the pipeline creates:
    // the encrypted textures (Formats), the reference mip textures, the fallbacks and the encrypted emission maps, and
    // counts an original texture as removed once no material on the avatar references it anymore.
    internal sealed class TextureMemoryEstimate
    {
        public long Added { get; private set; }
        public long Removed { get; private set; }
        public long Delta => Added - Removed;

        static readonly AssetManager assetManager = AssetManager.GetInstance();

        public static TextureMemoryEstimate Calculate(ShellProtector protector, bool useSmallMip)
        {
            var estimate = new TextureMemoryEstimate();
            int defaultFallback = protector.GetDefaultFallback();

            var encrypted = new Dictionary<Material, ShellProtector.MatOption>();
            foreach (var (material, option) in protector.GetActiveMaterials())
            {
                if (MaterialIssues.Check(material) == MaterialIssues.Issue.None)
                    encrypted[material] = option;
            }

            // Main textures, with the shader families they are encrypted for: lilToon and Poiyomi bake different secrets,
            // so a texture shared by both is encrypted twice (Pipeline.EncryptForOtherSecrets).
            var mainTextures = new Dictionary<Texture2D, HashSet<bool>>();
            var mipTextures = new HashSet<(int, int, bool)>();
            var fallbacks = new HashSet<(Texture2D, int)>();
            var encryptedEmission = new HashSet<(Material, string)>();
            long added = 0;

            foreach (var pair in encrypted)
            {
                Material material = pair.Key;
                var mainTexture = (Texture2D)material.mainTexture;
                if (!mainTextures.TryGetValue(mainTexture, out HashSet<bool> families))
                    mainTextures[mainTexture] = families = new HashSet<bool>();
                families.Add(assetManager.IsLilToon(material.shader));

                bool fullChain = mainTexture.format == TextureFormat.BC7;
                int mipSize = Math.Max(mainTexture.width, mainTexture.height);
                mipTextures.Add(fullChain ? (mainTexture.width, mainTexture.height, true) : (mipSize, mipSize, false));

                int fallback = pair.Value != null ? pair.Value.Fallback : defaultFallback;
                fallbacks.Add((mainTexture, fallback));

                int mask = pair.Value != null ? pair.Value.EmissionMask : 0;
                string[] maps = MaterialIssues.EmissionMaps(material);
                for (int slot = 0; slot < maps.Length; slot++)
                {
                    if ((mask & (1 << slot)) == 0 || !material.HasProperty(maps[slot]) || !(material.GetTexture(maps[slot]) is Texture2D map))
                        continue;
                    encryptedEmission.Add((material, maps[slot]));
                    if (MaterialIssues.IsSupportedEmission(map) && !EmissionEncryption.ReusesMainTexture(material, slot))
                        added += EmissionBytes(map);
                }
            }

            foreach (var pair in mainTextures)
                added += EncryptedBytes(pair.Key) * pair.Value.Count;
            foreach (var (width, height, fullChain) in mipTextures)
                added += MipTextureBytes(width, height, fullChain, useSmallMip);
            foreach (var (texture, option) in fallbacks)
                added += FallbackBytes(texture, option);

            // The textures the avatar's materials reference before and after the build. An encrypted material loses its
            // main texture, every other slot holding a protected texture, and its encrypted emission maps.
            var before = new HashSet<Texture2D>();
            var after = new HashSet<Texture2D>();
            if (protector.Descriptor != null)
            {
                foreach (Renderer renderer in protector.Descriptor.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null)
                            continue;
                        bool isEncrypted = encrypted.ContainsKey(material);
                        foreach (string property in material.GetTexturePropertyNames())
                        {
                            if (!(material.GetTexture(property) is Texture2D texture))
                                continue;
                            before.Add(texture);
                            if (isEncrypted && (mainTextures.ContainsKey(texture) || encryptedEmission.Contains((material, property))))
                                continue;
                            after.Add(texture);
                        }
                    }
                }
            }

            estimate.Added = added;
            estimate.Removed = before.Where(t => !after.Contains(t)).Sum(TextureBytes);
            return estimate;
        }

        // Formats/*.cs: RGB24 and RGBA32 keep their format; DXT splits into the index texture and an RGBA32 endpoint
        // texture with one texel per block; BC7 into an R8 code texture and an RGBA32 endpoint atlas.
        static long EncryptedBytes(Texture2D texture)
        {
            int width = texture.width, height = texture.height;
            switch (texture.format)
            {
                case TextureFormat.RGB24:
                case TextureFormat.RGBA32:
                    return Bytes(width, height, texture.format, Math.Max(1, MaxMipLevel(width, height) - 2));
                case TextureFormat.DXT1:
                case TextureFormat.DXT1Crunched:
                case TextureFormat.DXT5:
                case TextureFormat.DXT5Crunched:
                {
                    bool dxt5 = texture.format == TextureFormat.DXT5 || texture.format == TextureFormat.DXT5Crunched;
                    int mips = DxtMipCount(texture);
                    return Bytes(width, height, dxt5 ? TextureFormat.DXT5 : TextureFormat.DXT1, mips) + Bytes(width / 4, height / 4, TextureFormat.RGBA32, mips);
                }
                case TextureFormat.BC7:
                    try
                    {
                        var layout = new BC7TextureLayout(texture, texture.mipmapCount);
                        return Bytes(width, height, TextureFormat.R8, layout.MipCount) + layout.AtlasBytes;
                    }
                    catch (ArgumentException)
                    {
                        return 0;
                    }
                default:
                    return 0;
            }
        }

        // TextureEncryptManager.GenerateRefMipmap: a compressed square, or one uncompressed row for small mip textures.
        static long MipTextureBytes(int width, int height, bool fullChain, bool small)
        {
            int mips = fullChain ? 1 + (int)Mathf.Log(Mathf.Max(width, height), 2) : MaxMipLevel(width, height);
            if (small)
                return Bytes(fullChain ? Math.Max(width, height) : width, 1, TextureFormat.RGB24, mips);
            return Bytes(width, height, TextureFormat.DXT1, mips);
        }

        // TextureEncryptManager.GenerateFallback: a compressed square with mips; white and black are the package's textures.
        static long FallbackBytes(Texture2D texture, int option)
        {
            if (option == (int)ShellProtectorFallback.White || option == (int)ShellProtectorFallback.Black)
                return 0;
            if (texture.width < 128 || texture.height < 128)
                return 0;
            int size = option >= 2 && option <= 7 ? 1 << option : 32;
            bool alpha = GraphicsFormatUtility.HasAlphaChannel(texture.graphicsFormat);
            return Bytes(size, size, alpha ? TextureFormat.DXT5 : TextureFormat.DXT1, 1 + (int)Mathf.Log(size, 2));
        }

        // EmissionEncryption.Encrypt: DXT reuses the main texture's encoder, the rest become RGBA32 without the 1x1 mip.
        static long EmissionBytes(Texture2D map)
        {
            if (TextureEncryptManager.IsDXTFormat(map.format))
                return EncryptedBytes(map);
            int levels = Math.Min(map.mipmapCount, (int)Mathf.Log(Math.Max(map.width, map.height), 2));
            return Bytes(map.width, map.height, TextureFormat.RGBA32, Math.Max(1, levels));
        }

        static long TextureBytes(Texture2D texture)
        {
            TextureFormat format = texture.format;
            if (format == TextureFormat.DXT1Crunched)
                format = TextureFormat.DXT1;
            else if (format == TextureFormat.DXT5Crunched)
                format = TextureFormat.DXT5;
            else if (format == TextureFormat.ETC_RGB4Crunched)
                format = TextureFormat.ETC_RGB4;
            else if (format == TextureFormat.ETC2_RGBA8Crunched)
                format = TextureFormat.ETC2_RGBA8;
            return Bytes(texture.width, texture.height, format, texture.mipmapCount);
        }

        static long Bytes(int width, int height, TextureFormat format, int mipCount)
        {
            GraphicsFormat graphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(format, false);
            if (graphicsFormat == GraphicsFormat.None)
                return 0;
            long bytes = 0;
            for (int mip = 0; mip < mipCount; mip++)
                bytes += GraphicsFormatUtility.ComputeMipmapSize(Math.Max(1, width >> mip), Math.Max(1, height >> mip), graphicsFormat);
            return bytes;
        }

        // BaseTextureFormat.GetCanMipmapLevel
        static int MaxMipLevel(int width, int height)
        {
            if (width < 1 || height <= 1)
                return 0;
            return Math.Max((int)Mathf.Log(width, 2), (int)Mathf.Log(height, 2));
        }

        // DXTFormat.GetDXTMipCount: the levels with at least two blocks.
        static int DxtMipCount(Texture2D texture)
        {
            int count = 0;
            for (int mip = 0; mip < texture.mipmapCount; mip++)
            {
                int blocks = Math.Max(1, (texture.width >> mip) / 4) * Math.Max(1, (texture.height >> mip) / 4);
                if (blocks < 2)
                    break;
                count++;
            }
            return Math.Max(1, count);
        }

        public static string FormatSize(long bytes)
        {
            return (Math.Abs(bytes) / (1024.0 * 1024.0)).ToString("0.0") + " MB";
        }

        public static string FormatDelta(long bytes)
        {
            return (bytes < 0 ? "-" : "+") + FormatSize(bytes);
        }
    }
}
#endif
