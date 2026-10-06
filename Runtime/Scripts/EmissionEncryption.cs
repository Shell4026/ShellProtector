#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    // Emission uses independent blocks, dimensions and key domains. Main-map
    // format keywords and ChaCha nonce remain unchanged. The encrypted words use
    // the main RGBA32 layout (ChaCha: one keystream block per 4x4 texels, XXTEA:
    // pairs of texels), on the endpoint texture for DXT. Emission.cginc decrypts it.
    public static class EmissionEncryption
    {
        public static readonly string[] PoiyomiMaps = { "_EmissionMap", "_EmissionMap1", "_EmissionMap2", "_EmissionMap3" };
        public static readonly string[] LilToonMaps = { "_EmissionMap", "_Emission2ndMap" };
        public static string TextureProperty(int slot) => "_ShellEmission" + slot;
        public static string BlocksProperty(int slot) => TextureProperty(slot) + "Blocks";
        public static string SettingsProperty(int slot) => TextureProperty(slot) + "Settings";
        public static string WrapProperty(int slot) => TextureProperty(slot) + "Wrap";

        // False for a shader injected before emission support; it has to be injected again.
        public static bool SupportsEmission(Shader shader)
        {
            return shader != null && shader.FindPropertyIndex(BlocksProperty(0)) >= 0;
        }

        public static bool IsEmissionMap(Material material, string property)
        {
            string[] maps = AssetManager.GetInstance().IsPoiyomi(material.shader) ? PoiyomiMaps : LilToonMaps;
            return material.HasProperty(SettingsProperty(0)) && Array.IndexOf(maps, property) >= 0;
        }

        public static byte[] SlotKey(byte[] key, int slot)
        {
            var result = (byte[])key.Clone();
            uint domain = 0x53450000u + (uint)slot;
            for (int i = 0; i < 4; i++) result[i] ^= (byte)(domain >> (i * 8));
            return result;
        }

        public static EncryptResult Encrypt(Texture2D source, byte[] key, IEncryptor cipher, int slot)
        {
            if (!source.isReadable || !Mathf.IsPowerOfTwo(source.width) || !Mathf.IsPowerOfTwo(source.height) || source.width * source.height < 2)
                throw new InvalidOperationException("Emission texture must be readable, power-of-two, and contain at least two pixels: " + source.name);

            if (TextureEncryptManager.IsDXTFormat(source.format))
            {
                // Reuse the main texture's DXT1/DXT5 encoder: compressed selectors
                // and alpha stay in Texture1, encrypted RGB565 endpoints in Texture2.
                EncryptResult compressed = TextureEncryptManager.EncryptTexture(source, SlotKey(key, slot), cipher);
                compressed.Texture1.Apply(false, false);
                compressed.Texture2.Apply(false, false);
                return compressed;
            }
            return new EncryptResult { Texture1 = EncryptRGBA(source, key, cipher, slot) };
        }

        static Texture2D EncryptRGBA(Texture2D source, byte[] key, IEncryptor cipher, int slot)
        {
            // Omit the 1x1 mip: the ciphers operate on pairs of RGBA pixels.
            int levels = Math.Min(source.mipmapCount, (int)Mathf.Log(Math.Max(source.width, source.height), 2));
            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, levels, true)
            {
                name = source.name + "_emission_encrypted",
                filterMode = FilterMode.Point,
                anisoLevel = 0
            };
            try
            {
                byte[] slotKey = SlotKey(key, slot);
                uint[] words = BaseTextureFormat.ConvertKeyToUInt(slotKey);
                for (int mip = 0; mip < levels; mip++)
                {
                    Color32[] pixels = ReadPixels(source, mip);
                    if (cipher is Chacha20 chacha)
                    {
                        BaseTextureFormat.EncryptBlocks(pixels, Math.Max(1, source.width >> mip), Math.Max(1, source.height >> mip), mip, slotKey, chacha, true);
                    }
                    else
                    {
                        for (int i = 0; i < pixels.Length; i += 2)
                        {
                            words[3] = BaseTextureFormat.GetUnitKey(slotKey, (uint)i, mip);
                            uint[] encrypted = cipher.Encrypt(new[] { Pack(pixels[i]), Pack(pixels[i + 1]) }, words);
                            pixels[i] = Unpack(encrypted[0]);
                            pixels[i + 1] = Unpack(encrypted[1]);
                        }
                    }
                    result.SetPixels32(pixels, mip);
                }
                result.Apply(false, false);
                return result;
            }
            catch { UnityEngine.Object.DestroyImmediate(result); throw; }
        }

        static uint Pack(Color32 c) => (uint)(c.r | c.g << 8 | c.b << 16 | c.a << 24);
        static Color32 Unpack(uint c) => new Color32((byte)c, (byte)(c >> 8), (byte)(c >> 16), (byte)(c >> 24));

        static Color32[] ReadPixels(Texture2D source, int mip)
        {
            Color32[] pixels = source.GetPixels32(mip);
            int width = Math.Max(1, source.width >> mip);
            int height = Math.Max(1, source.height >> mip);
            if (pixels.Length == width * height) return pixels;
            // Unity may return the padded 4x4 DXT block for the smallest mips.
            int stride = Math.Max(4, width);
            if (!TextureEncryptManager.IsDXTFormat(source.format) || pixels.Length != stride * Math.Max(4, height))
                throw new InvalidOperationException($"Unexpected emission pixel count at mip {mip}: {pixels.Length}, expected {width * height} ({source.name}).");
            var cropped = new Color32[width * height];
            for (int y = 0; y < height; y++) Array.Copy(pixels, y * stride, cropped, y * width, width);
            return cropped;
        }

        // secrets: those compiled into the target's shader, the same ones its main texture is encrypted with.
        public static void Apply(Material source, Material target, byte[] key, IEncryptor cipher, ShaderSecrets secrets, AssetWriter writer, string folderGuid, int emissionMask = 0)
        {
            if (emissionMask == 0) return;
            if (cipher is Chacha20 chacha) chacha.Constants = secrets.ChachaConstants;
            key = secrets.MaskKey(key);
            bool poiyomi = AssetManager.GetInstance().IsPoiyomi(source.shader);
            string[] maps = poiyomi ? PoiyomiMaps : LilToonMaps;
            for (int slot = 0; slot < maps.Length; slot++)
            {
                if ((emissionMask & (1 << slot)) == 0) continue;
                string map = maps[slot];
                if (!source.HasProperty(map) || source.GetTexture(map) == null) continue;
                if (!(source.GetTexture(map) is Texture2D texture) || !TextureEncryptManager.IsSupportedTexture(texture))
                    throw new InvalidOperationException(source.name + ": unsupported emission texture in " + map);
                if (!target.HasProperty(TextureProperty(slot)))
                    throw new InvalidOperationException("Encrypted shader is missing emission support. Regenerate the shader: " + source.name);

                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                bool readable = importer != null && importer.isReadable;
                bool crunch = importer != null && importer.crunchedCompression;
                try
                {
                    if (importer != null && (!readable || crunch))
                    {
                        importer.isReadable = true;
                        importer.crunchedCompression = false;
                        importer.SaveAndReimport();
                    }
                    EncryptResult encrypted = Encrypt(texture, key, cipher, slot);
                    writer.CreateAssetInFolder(encrypted.Texture1, folderGuid, source.name + map + "_encrypted.asset");
                    target.SetTexture(TextureProperty(slot), encrypted.Texture1);
                    if (encrypted.Texture2 != null)
                    {
                        writer.CreateAssetInFolder(encrypted.Texture2, folderGuid, source.name + map + "_blocks.asset");
                        target.SetTexture(BlocksProperty(slot), encrypted.Texture2);
                    }
                    // Poiyomi shares the main texture's sampler with emission;
                    // lilToon uses the emission map's own sampler.
                    Texture sampler = poiyomi && source.mainTexture != null ? source.mainTexture : texture;
                    int format = texture.format == TextureFormat.DXT1 ? 2 : texture.format == TextureFormat.DXT5 ? 3 : 1;
                    target.SetVector(SettingsProperty(slot), new Vector4(format, texture.isDataSRGB ? 1 : 0, encrypted.Texture1.mipmapCount - 1, (int)sampler.filterMode));
                    target.SetVector(WrapProperty(slot), new Vector4((int)sampler.wrapModeU, (int)sampler.wrapModeV, sampler.mipMapBias, 0));
                    // Keep UV transforms, but remove the original image from the output material.
                    target.SetTexture(map, Texture2D.blackTexture);
                }
                finally
                {
                    if (importer != null && (importer.isReadable != readable || importer.crunchedCompression != crunch))
                    {
                        importer.isReadable = readable;
                        importer.crunchedCompression = crunch;
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        public static string Properties(int count)
        {
            string result = "";
            for (int i = 0; i < count; i++)
                result += $"\n[HideInInspector] {TextureProperty(i)} (\"Encrypted emission {i}\", 2D) = \"black\" {{}}\n" +
                    $"[HideInInspector] {BlocksProperty(i)} (\"Encrypted emission blocks {i}\", 2D) = \"black\" {{}}\n" +
                    $"[HideInInspector] {SettingsProperty(i)} (\"Emission settings\", Vector) = (0,0,0,0)\n" +
                    $"[HideInInspector] {WrapProperty(i)} (\"Emission wrap\", Vector) = (0,0,0,0)\n";
            return result;
        }
    }
}
#endif
