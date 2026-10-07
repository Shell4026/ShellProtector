#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    // Per material: encrypt the main texture, inject the decryption into a copy of the shader, and write the encrypted material.
    public sealed partial class Pipeline
    {
        readonly Dictionary<int, Texture2D> mipTextures = new Dictionary<int, Texture2D>();

        void EncryptMaterials()
        {
            List<Material> materials = settings.Materials.Where(IsSupportedFormat).ToList();
            int progress = 0;
            try
            {
                foreach (Material mat in materials)
                {
                    if (mat == null)
                        continue;

                    settings.MaterialOptions.TryGetValue(mat, out ShellProtector.MatOption option);
                    if (option != null && !option.Active)
                    {
                        Debug.LogFormat("{0} : Skip", mat.name);
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Encrypt...", "Encrypt Progress " + ++progress + " of " + materials.Count, (float)progress / materials.Count);
                    EncryptMaterial(mat, option);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        void EncryptMaterial(Material mat, ShellProtector.MatOption option)
        {
            Injector injector = InjectorFactory.GetInjector(mat.shader);
            if (injector == null)
            {
                Debug.LogError(mat.shader + " is a unsupported shader! supported type:lilToon, poiyomi");
                return;
            }
            if (!CanEncrypt(mat, injector))
                return;

            if (shaderManager.IsPoiyomi(mat.shader) && !shaderManager.IsLockPoiyomi(mat))
            {
                shaderManager.LockShader(mat);
                Debug.LogFormat("Lock: {0} - {1}", mat.name, AssetDatabase.GetAssetPath(mat.shader));
            }

            Debug.LogFormat("{0} : Start encrypt...", mat.name);

            Texture2D mainTexture = (Texture2D)mat.mainTexture;
            ShaderSecrets secrets = SelectShaderSecrets(mat);
            int filter = option != null ? option.Filter : settings.Filter;
            injector.Init(request.Avatar, mainTexture, Result.keyBytes, settings.KeySize, filter, settings.RuntimeDir, encryptor, secrets);

            ProcessedTexture processedTexture;
            Texture2D mipTexture;
            using (timings.Measure("textures"))
            {
                mipTexture = GetMipTexture(Math.Max(mainTexture.width, mainTexture.height));

                TextureSettings.SetRWEnableTexture(mainTexture);
                TextureSettings.SetCrunchCompression(mainTexture, false);
                TextureSettings.SetGenerateMipmap(mainTexture, true);

                ProcessedTexture? processed = GenerateEncryptedTexture(mat, mainTexture, secrets);
                if (!processed.HasValue)
                    return;
                processedTexture = processed.Value;
            }

            AuxiliaryTextures auxiliary = GetAuxiliaryTextures(mat);
            Shader encryptedShader;
            using (timings.Measure("shaders"))
            {
                encryptedShader = GetEncryptedShader(mat, injector, processedTexture.Encrypted.Texture1, auxiliary, secrets);
                if (encryptedShader == null)
                    return;
            }

            using (timings.Measure("materials"))
            {
                Texture2D fallback = GenerateFallbackTexture(paths.FallbackTextureName(mainTexture), option, mainTexture, ref processedTexture);
                if (fallback == null)
                    Debug.LogErrorFormat("Failed to generate fallback texture: {0}", mainTexture.name);

                GenerateEncryptedMaterial(mat, encryptedShader, fallback, mipTexture, auxiliary, processedTexture, secrets, injector);
            }
        }

        bool IsSupportedFormat(Material mat)
        {
            if (TextureEncryptManager.IsSupportedFormat(mat))
                return true;
            if (mat.mainTexture != null)
                Debug.LogWarningFormat("{0} : is unsupported format", mat.mainTexture.name);
            return false;
        }

        static bool CanEncrypt(Material mat, Injector injector)
        {
            if (mat.mainTexture == null)
            {
                Debug.LogWarningFormat("{0} : The mainTexture is empty. it will be skip.", mat.name);
                return false;
            }
            if ((mat.mainTexture is Texture2D) == false)
            {
                Debug.LogErrorFormat("MainTexture in {0} is not texture2D", mat.name);
                return false;
            }
            if (mat.mainTexture.width % 2 != 0 || mat.mainTexture.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", mat.mainTexture.name);
                return false;
            }
            if (injector.WasInjected(mat.shader))
            {
                Debug.LogWarning(mat.name + ": The shader is already encrypted.");
                return false;
            }
            return true;
        }

        // A Poiyomi copy bakes the secrets of its main texture, so materials sharing a texture share one encryption of it.
        // lilToon materials all use the project's shader (LilToonShaders).
        ShaderSecrets SelectShaderSecrets(Material mat)
        {
            if (shaderManager.IsLilToon(mat.shader))
                return LilToonShaders.GetSecrets();

            return history.GetTextureSecrets((Texture2D)mat.mainTexture);
        }

        // Reuses the Poiyomi copy injected by an earlier build if it bakes the same secrets; lilToon always takes the project's copy.
        Shader GetEncryptedShader(Material mat, Injector injector, Texture2D encryptedTexture, AuxiliaryTextures auxiliary, ShaderSecrets secrets)
        {
            Shader encryptedShader = shaderManager.IsLilToon(mat.shader) ? null : history.IsEncryptedBefore(mat.shader, secrets);
            if (EmissionEncryption.SupportsEmission(encryptedShader))
                return encryptedShader;

            try
            {
                string shaderDir = writer.ResolveFolderPath(paths.EnsureShaderFolder(writer, mat));
                encryptedShader = injector.Inject(mat, OutputPaths.Combine(settings.RuntimeDir, "Shader/Protector.cginc"), shaderDir, encryptedTexture, auxiliary);
                Selection.activeObject = encryptedShader;
                EditorApplication.ExecuteMenuItem("Assets/Reimport");
                if (encryptedShader == null)
                {
                    Debug.LogErrorFormat("{0}: Injection failed", mat.name);
                    return null;
                }
                history.Save(mat.shader, secrets);
                return encryptedShader;
            }
            catch (UnityException e)
            {
                Debug.LogError(e.Message);
                return null;
            }
        }

        AuxiliaryTextures GetAuxiliaryTextures(Material mat)
        {
            // LimTexture, OutlineTexture...
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

        // One reference mip texture per size, shared by the materials whose main textures have that size.
        Texture2D GetMipTexture(int size)
        {
            if (mipTextures.TryGetValue(size, out Texture2D mip))
                return mip;

            string fileName = paths.MipTextureName(size);
            mip = TextureEncryptManager.GenerateRefMipmap(size, size, settings.UseSmallMipTexture);
            if (mip == null)
                Debug.LogErrorFormat("{0} : Can't generate mip tex{1}.", fileName, size);
            else
            {
                writer.CreateAssetInFolder(mip, paths.Folders.TexGuid, fileName);
                writer.SaveAndRefresh();
            }
            mipTextures[size] = mip;
            return mip;
        }

        ProcessedTexture? GenerateEncryptedTexture(Material mat, Texture2D mainTexture, ShaderSecrets secrets)
        {
            bool processed = Result.processedTextures.TryGetValue(mainTexture, out ProcessedTexture processedTexture);
            if (!processed)
            {
                processedTexture = new ProcessedTexture
                {
                    Encrypted = new EncryptResult(),
                    Fallbacks = new List<Texture2D>(),
                    FallbackOptions = new List<int>(),
                    Nonce = new byte[12]
                };
            }

            // Set the ChaCha nonce: new for each texture, and the texture's own when it was already encrypted.
            if (encryptor is Chacha20 chacha)
            {
                if (!processed)
                {
                    byte[] hashMat = KeyGenerator.GetHash(mat.GetInstanceID());
                    for (int i = 0; i < chacha.Nonce.Length; ++i)
                        chacha.Nonce[i] ^= hashMat[i];
                    Array.Copy(chacha.Nonce, 0, processedTexture.Nonce, 0, processedTexture.Nonce.Length);
                }
                else
                {
                    Array.Copy(processedTexture.Nonce, 0, chacha.Nonce, 0, chacha.Nonce.Length);
                }
            }

            if (processed && !secrets.Matches(processedTexture.Secrets))
                return EncryptForOtherSecrets(mainTexture, processedTexture, secrets);

            if (!processed)
            {
                EncryptResult encryptResult;
                try
                {
                    encryptResult = TextureEncryptManager.EncryptTexture(mainTexture, Result.keyBytes, encryptor, secrets);
                }
                catch (ArgumentException e)
                {
                    Debug.LogErrorFormat("{0} : ArgumentException - {1}", mainTexture.name, e.Message);
                    return null;
                }
                writer.CreateAssetInFolder(encryptResult.Texture1, paths.Folders.TexGuid, paths.EncryptedTextureName(mainTexture, 0));
                if (encryptResult.Texture2 != null)
                    writer.CreateAssetInFolder(encryptResult.Texture2, paths.Folders.TexGuid, paths.EncryptedTextureName(mainTexture, 2));

                processedTexture.Encrypted = encryptResult;
                processedTexture.Secrets = secrets;

                Result.processedTextures.Add(mainTexture, processedTexture);
            }

            return processedTexture;
        }

        // The nonce and fallbacks stay those of the processed texture; only the encrypted textures differ.
        ProcessedTexture? EncryptForOtherSecrets(Texture2D mainTexture, ProcessedTexture processedTexture, ShaderSecrets secrets)
        {
            var key = (mainTexture, secrets.ToDefines());
            if (!Result.otherSecretsTextures.TryGetValue(key, out EncryptResult encryptResult))
            {
                try
                {
                    encryptResult = TextureEncryptManager.EncryptTexture(mainTexture, Result.keyBytes, encryptor, secrets);
                }
                catch (ArgumentException e)
                {
                    Debug.LogErrorFormat("{0} : ArgumentException - {1}", mainTexture.name, e.Message);
                    return null;
                }

                int variant = Result.otherSecretsTextures.Keys.Count(k => k.Item1 == mainTexture) + 1;
                writer.CreateAssetInFolder(encryptResult.Texture1, paths.Folders.TexGuid, paths.EncryptedTextureName(mainTexture, 0, variant));
                if (encryptResult.Texture2 != null)
                    writer.CreateAssetInFolder(encryptResult.Texture2, paths.Folders.TexGuid, paths.EncryptedTextureName(mainTexture, 2, variant));
                Result.otherSecretsTextures.Add(key, encryptResult);
            }

            processedTexture.Encrypted = encryptResult;
            processedTexture.Secrets = secrets;
            return processedTexture;
        }

        Texture2D GenerateFallbackTexture(string fileName, ShellProtector.MatOption option, Texture2D mainTexture, ref ProcessedTexture processedTexture)
        {
            int fallbackOption = option != null ? option.Fallback : settings.Fallback;

            int idx = processedTexture.FallbackOptions.FindIndex(o => o == fallbackOption);
            if (idx != -1)
                return processedTexture.Fallbacks[idx];

            Texture2D fallback;
            if (fallbackOption == (int)ShellProtectorFallback.White)
                fallback = fallbackWhite;
            else if (fallbackOption == (int)ShellProtectorFallback.Black)
                fallback = fallbackBlack;
            else
            {
                fallback = TextureEncryptManager.GenerateFallback(mainTexture, FallbackSize(fallbackOption));
                if (fallback == null)
                    return null;
                writer.CreateAssetInFolder(fallback, paths.Folders.TexGuid, fileName);
                writer.SaveAndRefresh();
            }

            processedTexture.Fallbacks.Add(fallback);
            processedTexture.FallbackOptions.Add(fallbackOption);
            return fallback;
        }

        // Options 2 to 7 are 4x4 to 128x128; anything else is 32x32.
        static int FallbackSize(int fallbackOption)
        {
            return fallbackOption >= 2 && fallbackOption <= 7 ? 1 << fallbackOption : 32;
        }

        void GenerateEncryptedMaterial(Material mat, Shader encryptedShader, Texture2D fallback, Texture2D mip, AuxiliaryTextures auxiliary, ProcessedTexture processedTexture, ShaderSecrets secrets, Injector injector)
        {
            MaterialEncryptor materialEncryptor = new MaterialEncryptor(writer, settings.TurnOnAllSafetyFallback, settings.Algorithm, settings.Rounds);
            int emissionMask = settings.MaterialOptions.TryGetValue(mat, out var option) && option != null ? option.EmissionMask : 0;
            Material newMat = materialEncryptor.CreateEncryptedMaterial(paths.Folders.MatGuid, paths.EncryptedMaterialName(mat), mat, encryptedShader, fallback, mip, auxiliary, processedTexture, Result.keyBytes, 16 - settings.KeySize, encryptor, injector, secrets, emissionMask);
            Debug.LogFormat("{0} : create encrypted material : {1}", mat.name, AssetDatabase.GetAssetPath(newMat));

            if (!Result.encryptedMaterials.ContainsKey(mat))
                Result.encryptedMaterials.Add(mat, newMat);
        }
    }
}
#endif
