#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Shell.Protector
{
    public sealed class OutputPaths
    {
        public const string TexFolder = "Tex";
        public const string MatFolder = "Mat";
        public const string ShaderFolder = "Shader";
        public const string AnimFolder = "Anim";
        public const string MeshFolder = "Mesh";

        readonly Dictionary<Material, string> shaderFolderGuids = new Dictionary<Material, string>();

        public OutputPaths(string root, GameObject avatar)
        {
            Root = Normalize(root);
            AvatarName = Sanitize(avatar != null ? avatar.name : "Avatar");
            Avatar = Combine(Root, AvatarName);
            Tex = Combine(Avatar, TexFolder);
            Mat = Combine(Avatar, MatFolder);
            Shader = Combine(Avatar, ShaderFolder);
            Anim = Combine(Avatar, AnimFolder);
            Mesh = Combine(Avatar, MeshFolder);
        }

        public string Root { get; }
        public string AvatarName { get; }
        public string Avatar { get; }
        public string Tex { get; }
        public string Mat { get; }
        public string Shader { get; }
        public string Anim { get; }
        public string Mesh { get; }
        public OutputFolders Folders { get; private set; }

        public string MipTexture(int size) => Combine(Tex, "mip_" + size + ".asset");
        public string MipTextureName(int size) => "mip_" + size + ".asset";
        public string EncryptedTexture(Texture2D texture, int index) => Combine(Tex, BaseName(texture) + "_encrypt" + (index == 0 ? "" : index.ToString()) + ".asset");
        public string EncryptedTextureName(Texture2D texture, int index) => BaseName(texture) + "_encrypt" + (index == 0 ? "" : index.ToString()) + ".asset";
        public string FallbackTexture(Texture2D texture) => Combine(Tex, BaseName(texture) + "_fallback.asset");
        public string FallbackTextureName(Texture2D texture) => BaseName(texture) + "_fallback.asset";
        public string EncryptedMaterial(Material material) => Combine(Mat, BaseName(material) + "_encrypted.mat");
        public string EncryptedMaterialName(Material material) => BaseName(material) + "_encrypted.mat";
        public string DuplicatedMaterial(Material material) => Combine(Mat, BaseName(material) + "_duplicated.mat");
        public string DuplicatedMaterialName(Material material) => BaseName(material) + "_duplicated.mat";
        public string ShaderDirectory(Material material) => Combine(Shader, BaseName(material));
        public string Parameters(string name) => Combine(Avatar, Sanitize(name) + ".asset");
        public string ParametersName(string name) => Sanitize(name) + ".asset";
        public string Controller(RuntimeAnimatorController controller) => Combine(Anim, BaseName(controller) + "_encrypted.controller");
        public string ControllerName(RuntimeAnimatorController controller) => BaseName(controller) + "_encrypted.controller";
        public string AnimationClip(AnimationClip clip, string suffix) => Combine(Anim, BaseName(clip) + suffix + ".anim");
        public string AnimationClipName(AnimationClip clip, string suffix) => BaseName(clip) + suffix + ".anim";
        public string MeshAsset(Mesh mesh) => Combine(Mesh, BaseName(mesh) + ".asset");
        public string MeshAssetName(Mesh mesh) => BaseName(mesh) + ".asset";
        public string History() => Combine(Root, "EncryptedHistory.asset");
        public string HistoryName() => "EncryptedHistory.asset";

        public OutputFolders PrepareFolders(AssetWriter writer, bool deleteExistingChildren)
        {
            string rootGuid = writer.EnsureFolderAndGetGuid(Root);
            string avatarGuid = writer.EnsureFolderAndGetGuid(Avatar);

            if (deleteExistingChildren)
            {
                writer.DeleteAsset(Anim);
                writer.DeleteAsset(Mat);
                writer.DeleteAsset(Shader);
                writer.DeleteAsset(Tex);
                writer.DeleteAsset(Mesh);
                writer.SaveAndRefresh();
            }

            Folders = new OutputFolders(
                rootGuid,
                avatarGuid,
                writer.EnsureFolderAndGetGuid(Tex),
                writer.EnsureFolderAndGetGuid(Mat),
                writer.EnsureFolderAndGetGuid(Shader),
                writer.EnsureFolderAndGetGuid(Anim),
                writer.EnsureFolderAndGetGuid(Mesh)
            );
            writer.SaveAndRefresh();
            shaderFolderGuids.Clear();
            return Folders;
        }

        public string EnsureShaderFolder(AssetWriter writer, Material material)
        {
            if (material != null && shaderFolderGuids.TryGetValue(material, out string guid))
                return guid;

            string folderGuid = writer.EnsureFolderAndGetGuid(ShaderDirectory(material));
            if (material != null)
                shaderFolderGuids[material] = folderGuid;
            return folderGuid;
        }

        public static string Combine(params string[] parts)
        {
            return Normalize(Path.Combine(parts));
        }

        public static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        public static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Asset";

            string cleaned = Regex.Replace(name.Trim(), @"[^\w\- .가-힣ぁ-んァ-ヶ一-龠]", "_");
            cleaned = Regex.Replace(cleaned, @"\s+", "_");
            cleaned = cleaned.Trim('.', '_');
            return string.IsNullOrEmpty(cleaned) ? "Asset" : cleaned;
        }

        static string BaseName(Object asset)
        {
            return Sanitize(asset != null ? asset.name : "Asset");
        }
    }

    public sealed class OutputFolders
    {
        public OutputFolders(string rootGuid, string avatarGuid, string texGuid, string matGuid, string shaderGuid, string animGuid, string meshGuid)
        {
            RootGuid = rootGuid;
            AvatarGuid = avatarGuid;
            TexGuid = texGuid;
            MatGuid = matGuid;
            ShaderGuid = shaderGuid;
            AnimGuid = animGuid;
            MeshGuid = meshGuid;
        }

        public string RootGuid { get; }
        public string AvatarGuid { get; }
        public string TexGuid { get; }
        public string MatGuid { get; }
        public string ShaderGuid { get; }
        public string AnimGuid { get; }
        public string MeshGuid { get; }
    }
}
#endif
