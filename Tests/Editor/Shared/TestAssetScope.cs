#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector.Tests
{
    internal static class TestAssetScope
    {
        public const string GeneratedRoot = "Assets/ShellProtector/Tests/__Generated";
        public const string DefaultGeneratedRoot = "Assets/ShellProtector/Generated";

        public static void Reset()
        {
            DeleteGeneratedRoot();
            DeleteDefaultGeneratedRoot();
            EnsureFolder(GeneratedRoot);
        }

        public static void DeleteGeneratedRoot()
        {
            if (AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                AssetDatabase.DeleteAsset(GeneratedRoot);
                AssetDatabase.Refresh();
            }
        }

        public static void DeleteDefaultGeneratedRoot()
        {
            if (AssetDatabase.IsValidFolder(DefaultGeneratedRoot))
            {
                AssetDatabase.DeleteAsset(DefaultGeneratedRoot);
                AssetDatabase.Refresh();
            }
        }

        public static void EnsureFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new ArgumentException("Generated test assets must live under Assets.", nameof(assetPath));

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        public static string CreateAsset<T>(T asset, string relativePath) where T : UnityEngine.Object
        {
            EnsureFolder(GeneratedRoot);
            string path = GeneratedRoot + "/" + relativePath.Replace('\\', '/');
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return path;
        }

        public static Texture2D CreatePatternTexture(int width, int height, TextureFormat format, bool alpha)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte r = (byte)((x * 17 + y * 3) & 0xFF);
                    byte g = (byte)((x * 5 + y * 29) & 0xFF);
                    byte b = (byte)((x * 11 + y * 7 + 31) & 0xFF);
                    byte a = alpha ? (byte)(64 + ((x * 13 + y * 19) & 0x7F)) : (byte)255;
                    pixels[y * width + x] = new Color32(r, g, b, a);
                }
            }
            if (format == TextureFormat.DXT1 || format == TextureFormat.DXT5)
                FillDxtStablePattern(pixels, width, height, alpha);

            texture.SetPixels32(pixels);
            texture.Apply(true, false);

            if (format == TextureFormat.RGB24)
            {
                Texture2D rgb = new Texture2D(width, height, TextureFormat.RGB24, true, true);
                rgb.SetPixels32(texture.GetPixels32());
                rgb.Apply(true, false);
                UnityEngine.Object.DestroyImmediate(texture);
                return rgb;
            }

            if (format == TextureFormat.RGBA32)
                return texture;

            TextureFormat uncompressedFormat = format == TextureFormat.DXT1 ? TextureFormat.RGB24 : TextureFormat.RGBA32;
            Texture2D dxt = new Texture2D(width, height, uncompressedFormat, true, true);
            dxt.SetPixels32(texture.GetPixels32());
            dxt.Apply(true, false);
            dxt.Compress(format == TextureFormat.DXT5);
            dxt.Apply(true, false);
            UnityEngine.Object.DestroyImmediate(texture);
            return dxt;
        }

        private static void FillDxtStablePattern(Color32[] pixels, int width, int height, bool alpha)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int block = (x / 4) + (y / 4) * (width / 4);
                    Color32 c0 = new Color32(
                        Expand5((block * 3 + 4) & 31),
                        Expand6((block * 5 + 9) & 63),
                        Expand5((block * 7 + 13) & 31),
                        alpha ? (byte)96 : (byte)255);
                    Color32 c1 = new Color32(
                        Expand5((block * 11 + 17) & 31),
                        Expand6((block * 13 + 21) & 63),
                        Expand5((block * 15 + 25) & 31),
                        alpha ? (byte)192 : (byte)255);
                    pixels[y * width + x] = ((x + y) & 1) == 0 ? c0 : c1;
                }
            }
        }

        private static byte Expand5(int value)
        {
            return (byte)((value * 255 + 15) / 31);
        }

        private static byte Expand6(int value)
        {
            return (byte)((value * 255 + 31) / 63);
        }

        public static Mesh CreateBlendShapeQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "ShellProtectorTestMesh";
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();

            Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
            Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
            Vector3[] deltaTangents = new Vector3[mesh.vertexCount];
            for (int i = 0; i < deltaVertices.Length; i++)
                deltaVertices[i] = new Vector3(0f, 0.01f * (i + 1), 0f);
            mesh.AddBlendShapeFrame("Smile", 100f, deltaVertices, deltaNormals, deltaTangents);
            return mesh;
        }

        public static void DestroyObjects(IEnumerable<UnityEngine.Object> objects)
        {
            foreach (UnityEngine.Object obj in objects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
#endif
