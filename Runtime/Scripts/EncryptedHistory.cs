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
        }

        // Bump whenever the encrypted texture layout changes; outputs from an older layout decrypt to noise.
        // 1: ChaCha 4x4 block keystream for RGB/RGBA, mip level mixed into every key.
        public const int CurrentFormatVersion = 1;

        // No initializer on purpose: histories saved before this field existed must deserialize as 0.
        [SerializeField]
        int formatVersion;
        [SerializeField]
        List<ShaderInfo> shaderHistory = new List<ShaderInfo>();
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

        public void Save(Shader shader)
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
            }
            else
            {
                var shaderInfo = new ShaderInfo { shader = shader.name, size = size, hash = hash };
                shaderHistoryDic.Add(shader, shaderInfo);
                shaderHistory.Add(shaderInfo);
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public Shader IsEncryptedBefore(Shader originalShader)
        {
            if (shaderHistoryDic.ContainsKey(originalShader))
            {
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
