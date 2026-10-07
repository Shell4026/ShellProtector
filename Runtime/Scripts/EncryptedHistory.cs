#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    [CreateAssetMenu(fileName = "EncryptedHistory", menuName = "ShellProtector/EncryptedHistory", order = 1)]
    public class EncryptedHistory : ScriptableObject
    {
        [Serializable]
        class ShaderInfo
        {
            public string shader;
            public long size;
            public string hash;
            // What the injected copy bakes in (ShaderSecrets). Encrypting with anything else decrypts to noise.
            public ShaderSecrets secrets;
        }

        [Serializable]
        class TextureSecrets
        {
            public string texture; // asset GUID
            public ShaderSecrets secrets;
        }

        // Bump whenever the encrypted texture layout changes; outputs from an older layout decrypt to noise.
        // 1: ChaCha 4x4 block keystream for RGB/RGBA, mip level mixed into every key.
        // 2: per-shader key mask and ChaCha constants (ShaderSecrets) compiled into the shaders.
        // 3: ChaCha DXT units cover 4x4 blocks (16x16 texels), one keystream word per block.
        // 4: ChaCha reduced from 8 to 6 rounds.
        public const int CurrentFormatVersion = 4;

        // No initializer on purpose: histories saved before this field existed must deserialize as 0.
        [SerializeField]
        int formatVersion;
        [SerializeField]
        List<ShaderInfo> shaderHistory = new List<ShaderInfo>();
        // Every Poiyomi copy whose material uses a texture bakes that texture's secrets, so a texture shared by
        // several materials is still encrypted once. They are kept across builds so the copies can be reused.
        [SerializeField]
        List<TextureSecrets> textureSecrets = new List<TextureSecrets>();
        Dictionary<Shader, ShaderInfo> shaderHistoryDic = new Dictionary<Shader, ShaderInfo>();

        public bool IsOutdatedFormat => formatVersion < CurrentFormatVersion;

        public static EncryptedHistory CreateCurrent()
        {
            var history = CreateInstance<EncryptedHistory>();
            history.formatVersion = CurrentFormatVersion;
            return history;
        }

        public void LoadData()
        {
            if (shaderHistoryDic.Count != 0)
                return;

            foreach (var info in shaderHistory)
            {
                Shader shader = Shader.Find(info.shader);
                if (shader)
                {
                    shaderHistoryDic.TryAdd(shader, info);
                }
            }
        }

        public ShaderSecrets GetTextureSecrets(Texture2D texture)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
            if (string.IsNullOrEmpty(guid))
                return ShaderSecrets.Generate();

            TextureSecrets entry = textureSecrets.Find(e => e.texture == guid);
            if (entry != null && entry.secrets != null && entry.secrets.IsValid)
                return entry.secrets;

            if (entry == null)
            {
                entry = new TextureSecrets { texture = guid };
                textureSecrets.Add(entry);
            }
            entry.secrets = ShaderSecrets.Generate();
            EditorUtility.SetDirty(this);
            return entry.secrets;
        }

        public void Save(Shader shader, ShaderSecrets secrets)
        {
            if (AssetManager.GetInstance().IsLilToon(shader))
                return;

            string dir = AssetDatabase.GetAssetPath(shader);
            FileInfo fileInfo = new FileInfo(dir);
            long size = fileInfo.Length;
            string hash = CalculateMD5(dir);

            if (shaderHistoryDic.ContainsKey(shader))
            {
                shaderHistoryDic[shader].size = size;
                shaderHistoryDic[shader].hash = hash;
                shaderHistoryDic[shader].secrets = secrets;
            }
            else
            {
                var shaderInfo = new ShaderInfo { shader = shader.name, size = size, hash = hash, secrets = secrets };
                shaderHistoryDic.Add(shader, shaderInfo);
                shaderHistory.Add(shaderInfo);
            }

            EditorUtility.SetDirty(this);
        }

        // With secrets, the copy is returned only if it bakes in those; otherwise it has to be injected again.
        public Shader IsEncryptedBefore(Shader originalShader, ShaderSecrets secrets = null)
        {
            if (shaderHistoryDic.ContainsKey(originalShader))
            {
                if (secrets != null && !secrets.Matches(shaderHistoryDic[originalShader].secrets))
                    return null;

                string dir = AssetDatabase.GetAssetPath(originalShader);
                FileInfo fileInfo = new FileInfo(dir);
                long size = fileInfo.Length;
                long oldSize = shaderHistoryDic[originalShader].size;
                string oldHash = shaderHistoryDic[originalShader].hash;

                if (size == oldSize)
                {
                    if (oldHash == CalculateMD5(dir))
                        return Shader.Find(originalShader.name + "_encrypted");
                    return null;
                }
                else
                    return null;
            }
            return null;
        }

        string CalculateMD5(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"File not found: {filePath}");
                return null;
            }

            using (FileStream stream = File.OpenRead(filePath))
            {
                MD5 md5 = MD5.Create();
                byte[] hashBytes = md5.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
#endif
