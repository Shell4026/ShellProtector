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
        string EnsureFolderAndGetGuid(string path);
        string ResolveFolderPath(string guid);
        string UniquePathInFolder(string folderGuid, string fileName);
        void CreateAssetInFolder(Object asset, string folderGuid, string fileName);
        bool CopyAssetToFolder(string sourcePath, string folderGuid, string fileName, out string targetPath);
    }

    public sealed class AssetWriter : IAssetWriter
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

        public string UniquePath(string path) => AssetDatabase.GenerateUniqueAssetPath(OutputPaths.Normalize(path));

        public string EnsureFolderAndGetGuid(string path)
        {
            string normalized = OutputPaths.Normalize(path).TrimEnd('/');
            EnsureFolder(normalized);

            string guid = AssetDatabase.AssetPathToGUID(normalized);
            if (string.IsNullOrEmpty(guid))
            {
                SaveAndRefresh();
                guid = AssetDatabase.AssetPathToGUID(normalized);
            }

            if (string.IsNullOrEmpty(guid))
                throw new System.InvalidOperationException("Failed to resolve folder GUID: " + normalized);

            return guid;
        }

        public string ResolveFolderPath(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                throw new System.InvalidOperationException("Failed to resolve folder path from GUID: " + guid);
            return OutputPaths.Normalize(path);
        }

        public string UniquePathInFolder(string folderGuid, string fileName)
        {
            string folder = ResolveFolderPath(folderGuid);
            return AssetDatabase.GenerateUniqueAssetPath(OutputPaths.Combine(folder, OutputPaths.Sanitize(fileName)));
        }

        public void CreateAssetInFolder(Object asset, string folderGuid, string fileName)
        {
            CreateAsset(asset, UniquePathInFolder(folderGuid, fileName));
        }

        public bool CopyAssetToFolder(string sourcePath, string folderGuid, string fileName, out string targetPath)
        {
            targetPath = UniquePathInFolder(folderGuid, fileName);
            return CopyAsset(sourcePath, targetPath);
        }

        public void EnsureFolder(string path)
        {
            string normalized = OutputPaths.Normalize(path).TrimEnd('/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return;

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
