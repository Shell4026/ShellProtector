using System;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector.Diagnostics
{
    internal static class TextureDiagnostics
    {
        internal static readonly byte[] Key = KeyGenerator.MakeKeyBytes("bc7-fixture", "test", 12);

        internal static Texture2D Pattern(int width, int height, bool alpha, bool srgb)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true, !srgb);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                    pixels[y * width + x] = new Color32((byte)(x * 255 / Math.Max(1, width - 1)),
                        (byte)(y * 255 / Math.Max(1, height - 1)), (byte)((x * 17 + y * 13) & 255),
                        alpha ? (byte)((x * 11 + y * 7) & 255) : (byte)255);
            texture.SetPixels32(pixels); texture.Apply(true, false);
            EditorUtility.CompressTexture(texture, TextureFormat.BC7, TextureCompressionQuality.Fast);
            texture.Apply(false, false);
            texture.name = "BC7_Pattern";
            return texture;
        }

        internal static Texture2D LinearCopy(Texture2D source)
        {
            var result = new Texture2D(source.width, source.height, TextureFormat.BC7, source.mipmapCount, true);
            result.LoadRawTextureData(source.GetRawTextureData()); result.Apply(false, false);
            result.filterMode = FilterMode.Point; result.wrapModeU = source.wrapModeU; result.wrapModeV = source.wrapModeV;
            return result;
        }

        internal static Color32[] Render(Texture source, Material material, int pass, int width, int height)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                using (new SynchronousShaderCompilation()) Graphics.Blit(source, target, material, pass);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0); readback.Apply(false, false);
                foreach (var message in ShaderUtil.GetShaderMessages(material.shader))
                    if (message.severity.ToString() == "Error") throw new InvalidOperationException(message.message);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(readback);
            }
        }

    }
}
