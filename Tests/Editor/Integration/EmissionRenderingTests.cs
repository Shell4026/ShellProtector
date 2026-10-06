#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shell.Protector.Tests.Integration
{
    public class EmissionRenderingTests
    {
        string root;
        readonly List<Object> objects = new List<Object>();
        readonly AssetWriter writer = new AssetWriter();

        [SetUp]
        public void SetUp()
        {
            string script = AssetDatabase.FindAssets("EmissionRenderingTests t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).First();
            root = Path.GetDirectoryName(script).Replace('\\', '/') + "/__Emission_" + Guid.NewGuid().ToString("N");
            writer.EnsureFolderAndGetGuid(root);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in objects) if (item != null && !AssetDatabase.Contains(item)) Object.DestroyImmediate(item);
            objects.Clear();
            if (!string.IsNullOrEmpty(root)) AssetDatabase.DeleteAsset(root);
        }

        [TestCase(false, 0, false, FilterMode.Point, TextureFormat.RGBA32)]
        [TestCase(false, 1, true, FilterMode.Bilinear, TextureFormat.DXT5)]
        [TestCase(false, 0, true, FilterMode.Trilinear, TextureFormat.DXT1)]
        [TestCase(false, 0, false, FilterMode.Bilinear, TextureFormat.DXT5)]
        [TestCase(true, 0, false, FilterMode.Point, TextureFormat.DXT1)]
        [TestCase(true, 1, true, FilterMode.Bilinear, TextureFormat.DXT5)]
        [TestCase(true, 2, false, FilterMode.Bilinear, TextureFormat.RGBA32)]
        [TestCase(true, 3, true, FilterMode.Bilinear, TextureFormat.RGBA32)]
        [TestCase(true, 0, true, FilterMode.Trilinear, TextureFormat.DXT5)]
        [TestCase(true, 2, false, FilterMode.Bilinear, TextureFormat.DXT5)]
        public void Emission_RoundTripAndWrongKey(bool poiyomi, int slot, bool xxtea, FilterMode filter, TextureFormat format)
        {
            RenderEmission(poiyomi, slot, xxtea, filter, format, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Emission_DefaultOff_PreservesOriginalEvenWithWrongKey(bool poiyomi)
        {
            RenderEmission(poiyomi, 0, false, FilterMode.Bilinear, TextureFormat.RGBA32, false);
        }

        void RenderEmission(bool poiyomi, int slot, bool xxtea, FilterMode filter, TextureFormat format, bool encrypt)
        {
            Shader shader = Shader.Find(poiyomi ? ".poiyomi/Poiyomi Toon" : "lilToon");
            Assert.That(shader, Is.Not.Null, "Install the supported shader to run this integration test.");
            var source = new Material(shader);
            source.name = "EmissionTest";
            objects.Add(source);
            var main = Pattern(128, 128, false);
            var emission = Pattern(64, 32, true, format);
            emission.wrapMode = TextureWrapMode.Mirror;
            emission.filterMode = filter;
            main.filterMode = filter;
            // Compare supported isotropic filtering even when the host project's
            // quality setting forces anisotropy on textures with anisoLevel > 0.
            main.anisoLevel = emission.anisoLevel = 0;
            main.mipMapBias = emission.mipMapBias = filter == FilterMode.Trilinear ? 0.25f : 0;
            if (xxtea)
            {
                // Main format and dimensions must not control emission decoding.
                main.Compress(false);
                main.Apply(false);
            }
            if (format == TextureFormat.DXT1)
            {
                var pixels = emission.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) pixels[i].a = 255;
                emission.SetPixels32(pixels);
                emission.Apply();
            }
            if (format == TextureFormat.DXT1 || format == TextureFormat.DXT5)
            {
                emission.Compress(false);
                emission.Apply(false);
                Assert.That(emission.format, Is.EqualTo(format));
            }
            source.mainTexture = main;
            source.SetColor("_Color", Color.black);
            string map = poiyomi ? EmissionEncryption.PoiyomiMaps[slot] : EmissionEncryption.LilToonMaps[slot];
            source.SetTexture(map, emission);
            string[] maps = poiyomi ? EmissionEncryption.PoiyomiMaps : EmissionEncryption.LilToonMaps;
            string unselected = maps[(slot + 1) % maps.Length];
            source.SetTexture(unselected, emission);
            source.SetTextureScale(map, filter == FilterMode.Trilinear ? new Vector2(5.3f, 4.7f) : new Vector2(1.3f, 0.7f));
            source.SetTextureOffset(map, new Vector2(-0.1f, 0.2f));
            if (poiyomi)
            {
                string suffix = slot == 0 ? "" : slot.ToString();
                source.SetFloat("_EnableEmission" + suffix, 1);
                source.SetFloat("_EmissionStrength" + suffix, 1);
                source.SetColor("_EmissionColor" + suffix, Color.white);
            }
            else
            {
                string prefix = slot == 0 ? "_Emission" : "_Emission2nd";
                source.SetFloat(slot == 0 ? "_UseEmission" : "_UseEmission2nd", 1);
                source.SetColor(prefix + "Color", Color.white);
                source.SetFloat(prefix + "Blend", 1);
            }
            AssetDatabase.CreateAsset(source, root + "/source.mat");
            if (poiyomi) AssetManager.GetInstance().LockShader(source);
            AssetDatabase.SaveAssets();

            Color32[] reference = SupportedShaderRenderingTests.RenderMaterial(source);
            Assert.That(reference.Average(c => (c.r + c.g + c.b) / 3.0), Is.GreaterThan(10), "Reference emission must be visible.");
            byte[] key = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            IEncryptor cipher = xxtea ? (IEncryptor)new XXTEA() : new Chacha20();
            Injector injector = InjectorFactory.GetInjector(source.shader);
            string runtime = Path.GetDirectoryName(AssetDatabase.FindAssets("Protector").Select(AssetDatabase.GUIDToAssetPath).First(p => p.EndsWith("/Shader/Protector.cginc"))).Replace('\\', '/');
            // Poiyomi copies compile in the secrets they are given, the lilToon copy the project's.
            ShaderSecrets secrets = poiyomi ? TestKeys.Secrets : LilToonShaders.GetSecrets();
            injector.Init(null, main, key, 0, 1, Path.GetDirectoryName(runtime).Replace('\\', '/'), cipher, secrets);
            var encryptedMain = TextureEncryptManager.EncryptTexture(main, key, cipher, secrets);
            encryptedMain.Texture1.Apply(false);
            objects.Add(encryptedMain.Texture1);
            Shader encryptedShader = injector.Inject(source, runtime + "/Protector.cginc", root + "/Shader", encryptedMain.Texture1);
            Assert.That(encryptedShader, Is.Not.Null);
            var target = new Material(source) { shader = encryptedShader };
            objects.Add(target);
            target.SetTexture("_EncryptTex0", encryptedMain.Texture1);
            var mip = TextureEncryptManager.GenerateRefMipmap(128, 128);
            mip.Apply(false);
            objects.Add(mip);
            target.SetTexture("_MipTex", mip);
            var (widthOffset, heightOffset) = TextureEncryptManager.CalculateOffsets(main);
            target.SetInteger("_Woffset", widthOffset);
            target.SetInteger("_Hoffset", heightOffset);
            if (encryptedMain.Texture2 != null)
            {
                encryptedMain.Texture2.Apply(false);
                objects.Add(encryptedMain.Texture2);
                target.SetTexture("_EncryptTex1", encryptedMain.Texture2);
            }
            for (int i = 0; i < 16; i++) target.SetFloat("_Key" + i, key[i]);
            target.SetInteger("_HashMagic", 42);
            target.SetInteger("_PasswordHash", unchecked((int)KeyGenerator.SimpleHash(key, 42)));
            injector.SetKeywords(target);
            int assetCount = AssetDatabase.FindAssets("t:Texture", new[] { root }).Length;
            if (encrypt)
                EmissionEncryption.Apply(source, target, key, cipher, secrets, writer, AssetDatabase.AssetPathToGUID(root), 1 << slot);
            else
                EmissionEncryption.Apply(source, target, key, cipher, secrets, writer, AssetDatabase.AssetPathToGUID(root));
            // Poiyomi's optimizer clears maps belonging to disabled features.
            Assert.That(target.HasProperty(unselected), Is.EqualTo(source.HasProperty(unselected)));
            if (source.HasProperty(unselected))
                Assert.That(target.GetTexture(unselected), Is.EqualTo(source.GetTexture(unselected)), "Unselected slot must remain untouched.");
            Assert.That(source.GetTexture(map), Is.EqualTo(emission));
            Assert.That(target.GetTextureScale(map), Is.EqualTo(source.GetTextureScale(map)));
            if (encrypt)
            {
                Assert.That(target.GetTexture(map), Is.Not.EqualTo(emission));
                var encryptedEmission = (Texture2D)target.GetTexture(EmissionEncryption.TextureProperty(slot));
                Assert.That(encryptedEmission.width, Is.EqualTo(64));
                Assert.That(encryptedEmission.format, Is.EqualTo(format));
                if (format == TextureFormat.DXT1 || format == TextureFormat.DXT5)
                    Assert.That(target.GetTexture(EmissionEncryption.BlocksProperty(slot)).width, Is.EqualTo(16));
            }
            else
            {
                Assert.That(target.GetTexture(map), Is.EqualTo(emission));
                Assert.That(target.GetVector(EmissionEncryption.SettingsProperty(slot)).x, Is.Zero);
                Assert.That(AssetDatabase.FindAssets("t:Texture", new[] { root }).Length, Is.EqualTo(assetCount));
                var avatar = new GameObject("Cleanup regression");
                objects.Add(avatar);
                avatar.AddComponent<MeshRenderer>().sharedMaterial = target;
                var protector = avatar.AddComponent<ShellProtector>();
                protector.AssetDir = root + "/Pipeline";
                var result = (BuildResult)typeof(ShellProtector).GetProperty("CurrentBuildResult", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(protector);
                result.EncryptedMaterials[source] = target;
                result.ProcessedTextures[emission] = new ProcessedTexture
                {
                    Encrypted = encryptedMain,
                    Fallbacks = new List<Texture2D> { Texture2D.blackTexture },
                    FallbackOptions = new List<int> { 0 }
                };
                protector.RemoveDuplicatedTextures(avatar);
                Assert.That(avatar.GetComponent<MeshRenderer>().sharedMaterial.GetTexture(map), Is.EqualTo(emission), "Fallback cleanup must preserve unselected emission maps shared with protected main maps.");
            }
            Color32[] actual = SupportedShaderRenderingTests.RenderMaterial(target);
            Assert.That(ShaderUtil.ShaderHasError(encryptedShader), Is.False, encryptedShader.name);
            SupportedShaderRenderingTests.AssertRenderedRgbClose(reference, actual, map);
            target.SetFloat("_Key0", key[0] + 1);
            Color32[] locked = SupportedShaderRenderingTests.RenderMaterial(target);
            if (encrypt)
                Assert.That(locked.Average(c => (c.r + c.g + c.b) / 3.0), Is.LessThan(2), "Wrong keys must hide selected emission.");
            else
                SupportedShaderRenderingTests.AssertRenderedRgbClose(reference, locked, "Unselected emission");
        }

        Texture2D Pattern(int width, int height, bool emission, TextureFormat format = TextureFormat.RGBA32)
        {
            var texture = new Texture2D(width, height, format == TextureFormat.DXT1 ? TextureFormat.RGB24 : TextureFormat.RGBA32, true, false);
            objects.Add(texture);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = emission ? new Color32((byte)(64 + x * 2), (byte)(64 + y * 4), 128, (byte)(128 + x % 128)) : new Color32(0,0,0,255);
            texture.SetPixels32(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, root + "/" + (emission ? "emission" : "main") + ".asset");
            return texture;
        }

        [Test]
        public void Injection_PreservesNestedUVAndPanning()
        {
            string input = "float4 e = POI2D_SAMPLER_PAN(_EmissionMap3, _MainTex, poiUV(float2(1, 2), st), pan);";
            string output = EmissionShaderInjector.InjectPoiyomi(input);
            Assert.That(output, Does.Contain("SHELL_EMISSION_SAMPLE_OR(3, (POI_PAN_UV(poiUV(float2(1, 2), st), pan))"));
            Assert.That(output, Does.Not.Contain("?"), "?: evaluates both sides, so unencrypted slots would decrypt too.");
            Assert.That(output, Does.Contain(input.Substring("float4 e = ".Length).TrimEnd(';')));
        }

        [TestCase(false, 2, 1)]
        [TestCase(true, 1, 8)]
        [TestCase(false, 64, 32)]
        [TestCase(true, 32, 64)]
        public void EncryptedRGBA_AllMipsRoundTrip(bool xxtea, int width, int height)
        {
            Texture2D source = Pattern(width, height, true);
            byte[] key = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            IEncryptor cipher = xxtea ? (IEncryptor)new XXTEA() : new Chacha20();
            Texture2D encrypted = EmissionEncryption.Encrypt(source, key, cipher, 1).Texture1;
            objects.Add(encrypted);
            byte[] slotKey = EmissionEncryption.SlotKey(key, 1);
            for (int mip = 0; mip < encrypted.mipmapCount; mip++)
            {
                uint[] expected = source.GetPixels32(mip).Select(Pack).ToArray();
                uint[] actual = encrypted.GetPixels32(mip).Select(Pack).ToArray();
                Assert.That(actual, Is.Not.EqualTo(expected), "Mip " + mip + " must be encrypted.");
                Assert.That(DecryptWords(actual, Math.Max(1, width >> mip), mip, slotKey, cipher), Is.EqualTo(expected), "Mip " + mip);
            }
            Assert.That(EmissionEncryption.SlotKey(key, 0), Is.Not.EqualTo(slotKey));
        }

        static uint Pack(Color32 c) => (uint)(c.r | c.g << 8 | c.b << 16 | c.a << 24);

        // Reference decryption of the RGBA32 layout that Emission.cginc reads: ChaCha encrypts 4x4 texels with one
        // keystream block, XXTEA pairs of texels; the unit index and the mip level go in the last key word.
        static uint[] DecryptWords(uint[] words, int width, int mip, byte[] key, IEncryptor cipher)
        {
            int height = words.Length / width;
            uint[] result = new uint[words.Length];
            uint[] unitKey = Enumerable.Range(0, 4).Select(i => BitConverter.ToUInt32(key, i * 4)).ToArray();
            uint last = unitKey[3];
            if (cipher is Chacha20)
            {
                int blocksPerRow = (width + 3) / 4;
                for (int by = 0; by < (height + 3) / 4; by++)
                    for (int bx = 0; bx < blocksPerRow; bx++)
                    {
                        unitKey[3] = last ^ (uint)(by * blocksPerRow + bx) ^ ((uint)mip << 24);
                        uint[] stream = cipher.Encrypt(new uint[16], unitKey);
                        for (int j = 0; j < 16; j++)
                        {
                            int x = bx * 4 + (j & 3), y = by * 4 + (j >> 2);
                            if (x < width && y < height)
                                result[y * width + x] = words[y * width + x] ^ stream[j];
                        }
                    }
                return result;
            }
            for (int i = 0; i < words.Length; i += 2)
            {
                unitKey[3] = last ^ (uint)i ^ ((uint)mip << 24);
                uint[] pair = cipher.Decrypt(new[] { words[i], words[i + 1] }, unitKey);
                result[i] = pair[0];
                result[i + 1] = pair[1];
            }
            return result;
        }

        [TestCase(TextureFormat.DXT1, 8, 4, false, false)]
        [TestCase(TextureFormat.DXT5, 8, 4, false, true)]
        [TestCase(TextureFormat.DXT1, 128, 8, true, true)]
        [TestCase(TextureFormat.DXT5, 8, 128, true, false)]
        [TestCase(TextureFormat.DXT1, 64, 32, true, false)]
        [TestCase(TextureFormat.DXT5, 32, 64, true, true)]
        public void DXT_ReusesCompressedBlocksAndRoundTripsEveryMip(TextureFormat format, int width, int height, bool mips, bool xxtea)
        {
            var source = new Texture2D(width, height, format == TextureFormat.DXT1 ? TextureFormat.RGB24 : TextureFormat.RGBA32, mips, false);
            objects.Add(source);
            Color32[] pixels = Enumerable.Range(0, width * height).Select(i => new Color32((byte)(i * 17), (byte)(i * 7), (byte)(i * 11), (byte)(128 + i % 128))).ToArray();
            source.SetPixels32(pixels);
            source.Apply();
            source.Compress(false);
            source.Apply(false);
            Assert.That(source.format, Is.EqualTo(format));
            byte[] raw = source.GetRawTextureData();
            byte[] key = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            IEncryptor cipher = xxtea ? (IEncryptor)new XXTEA() : new Chacha20();
            EncryptResult encrypted = EmissionEncryption.Encrypt(source, key, cipher, 2);
            objects.Add(encrypted.Texture1);
            objects.Add(encrypted.Texture2);
            Assert.That(encrypted.Texture1.format, Is.EqualTo(format));
            Assert.That(encrypted.Texture2.width, Is.EqualTo(width / 4));
            Assert.That(source.GetRawTextureData(), Is.EqualTo(raw), "Original compressed data must remain unchanged.");
            byte[] slotKey = EmissionEncryption.SlotKey(key, 2);
            int stride = format == TextureFormat.DXT1 ? 8 : 16;
            int colorOffset = format == TextureFormat.DXT1 ? 0 : 8;
            int offset = 0;
            byte[] selectors = encrypted.Texture1.GetRawTextureData();
            for (int mip = 0; mip < encrypted.Texture1.mipmapCount; mip++)
            {
                Color32[] blocks = encrypted.Texture2.GetPixels32(mip);
                uint[] decoded = DecryptWords(blocks.Select(Pack).ToArray(), Math.Max(1, encrypted.Texture2.width >> mip), mip, slotKey, cipher);
                for (int i = 0; i < blocks.Length; i++)
                    Assert.That(decoded[i], Is.EqualTo(BitConverter.ToUInt32(raw, offset + i * stride + colorOffset)), "Mip " + mip + ", block " + i);
                for (int i = 0; i < blocks.Length * stride; i++)
                    if (i % stride < colorOffset || i % stride >= colorOffset + 4)
                        Assert.That(selectors[offset + i], Is.EqualTo(raw[offset + i]), "Selectors and DXT5 alpha must be retained.");
                offset += blocks.Length * stride;
            }
        }

        [Test]
        public void MaterialOptions_DefaultOffAndPersistSelectedSlots()
        {
            Assert.That(new ShellProtector.MatOption().EmissionMask, Is.Zero);
            Assert.That(new ShellProtector.MatOption { EmissionEnc = true }.EmissionMask, Is.Zero, "Legacy flag must not opt in slots.");
            var owner = new GameObject("Emission options");
            objects.Add(owner);
            var protector = owner.AddComponent<ShellProtector>();
            var material = new Material(Shader.Find("lilToon"));
            objects.Add(material);
            AssetDatabase.CreateAsset(material, root + "/options.mat");
            protector.MaterialOptions[material] = new ShellProtector.MatOption { EmissionMask = 5 };
            protector.SaveMatOption();
            protector.SaveMatOption();
            Assert.That(new SerializedObject(protector).FindProperty("_matOptionSaved").arraySize, Is.EqualTo(1));
            PrefabUtility.SaveAsPrefabAsset(owner, root + "/options.prefab");
            var restoredOwner = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(root + "/options.prefab"));
            objects.Add(restoredOwner);
            var restored = restoredOwner.GetComponent<ShellProtector>();
            restored.SyncMatOption();
            Assert.That(restored.MaterialOptions[material].EmissionMask, Is.EqualTo(5));
            restored.ResetMaterialOptions();
            Assert.That(restored.MaterialOptions, Is.Empty);
        }
    }
}
#endif
