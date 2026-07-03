#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    public interface IAssetWriter
    {
        void CreateAsset(Object asset, string path);
        bool CopyAsset(string sourcePath, string targetPath);
        T LoadAssetAtPath<T>(string path) where T : Object;
        bool IsValidFolder(string path);
        void CreateFolder(string parentFolder, string newFolderName);
        void DeleteAsset(string path);
        void SaveAssets();
        void Refresh();
        void SaveAndRefresh();
    }

    public sealed class UnityAssetWriter : IAssetWriter
    {
        public void CreateAsset(Object asset, string path) => AssetDatabase.CreateAsset(asset, path);
        public bool CopyAsset(string sourcePath, string targetPath) => AssetDatabase.CopyAsset(sourcePath, targetPath);
        public T LoadAssetAtPath<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        public bool IsValidFolder(string path) => AssetDatabase.IsValidFolder(path);
        public void CreateFolder(string parentFolder, string newFolderName) => AssetDatabase.CreateFolder(parentFolder, newFolderName);
        public void DeleteAsset(string path) => AssetDatabase.DeleteAsset(path);
        public void SaveAssets() => AssetDatabase.SaveAssets();
        public void Refresh() => AssetDatabase.Refresh();

        public void SaveAndRefresh()
        {
            SaveAssets();
            Refresh();
        }
    }
}
#endif
