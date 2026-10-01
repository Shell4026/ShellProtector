#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector.Tests
{
    internal static class Bc7TestData
    {
        internal static readonly byte[] Key = KeyGenerator.MakeKeyBytes("bc7-fixture", "test", 12);

        // Independent control-field specification. Expected colors come from the GPU's native decoder.
        static readonly int[] PartBits = {4,6,6,6,0,0,0,6};
        static readonly int[] Sets = {3,2,3,2,1,1,1,2};
        static readonly int[] RgbBits = {4,6,5,7,5,7,7,5};
        static readonly int[] ABits = {0,0,0,0,6,8,7,5};
        static readonly int[] PCount = {6,2,0,4,0,0,2,4};

        internal static Texture2D ModeTexture(int mode)
        {
            int variants = (1 << PartBits[mode]) * (1 << PCount[mode]) * (mode == 4 ? 8 : mode == 5 ? 4 : 1);
            int columns = Math.Min(32, variants), rows = (variants + columns - 1) / columns;
            // The minimum test image is 8x8 so atlas overhead is below its RGBA equivalent.
            columns = Math.Max(2, columns); rows = Math.Max(2, rows);
            var raw = new byte[columns * rows * 16];
            var random = new System.Random(2700 + mode);
            random.NextBytes(raw);
            for (int block = 0; block < columns * rows; ++block)
            {
                int offset = block * 16;
                raw[offset] = (byte)((raw[offset] & ~((1 << (mode + 1)) - 1)) | (1 << mode));
                int variant = block % variants, p = mode + 1;
                int partition = variant % (1 << PartBits[mode]); variant >>= PartBits[mode];
                Write(raw, offset, ref p, PartBits[mode], partition);
                if (mode == 4 || mode == 5) { Write(raw, offset, ref p, 2, variant & 3); variant >>= 2; }
                if (mode == 4) { Write(raw, offset, ref p, 1, variant & 1); variant >>= 1; }
                p += 2 * Sets[mode] * (3 * RgbBits[mode] + ABits[mode]);
                Write(raw, offset, ref p, PCount[mode], variant);
            }
            var texture = new Texture2D(columns * 4, rows * 4, TextureFormat.BC7, false, true);
            texture.name = "BC7_Mode" + mode;
            texture.LoadRawTextureData(raw); texture.Apply(false, false); texture.filterMode = FilterMode.Point;
            return texture;
        }

        static void Write(byte[] data, int start, ref int position, int bits, int value)
        {
            for (int i = 0; i < bits; ++i, ++position)
            {
                int p = start + position / 8, mask = 1 << (position & 7);
                data[p] = (byte)((data[p] & ~mask) | (((value >> i) & 1) << (position & 7)));
            }
        }

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

        internal static Material Material(Texture2D source, EncryptResult encrypted)
        {
            Shader shader = Shader.Find("Hidden/ShellProtector/BC7Test");
            if (shader == null) throw new InvalidOperationException("BC7 test shader was not imported.");
            var material = new Material(shader);
            MaterialEncryptor.ConfigureDecryption(material, source, encrypted, Key, Key.Length, 2700);
            material.SetVector("_ReferenceSize", new Vector4(1f / source.width, 1f / source.height, source.width, source.height));
            material.SetInteger("_ReferenceSrgb", UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat) ? 1 : 0);
            return material;
        }

        internal static Color32[] Render(Texture source, Material material, int pass, int width, int height)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create(); Graphics.Blit(source, target, material, pass); RenderTexture.active = target;
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

        internal static int MaxError(Color32[] a, Color32[] b)
        {
            int error = 0;
            for (int i = 0; i < a.Length; ++i)
            {
                error = Math.Max(error, Math.Abs(a[i].r - b[i].r)); error = Math.Max(error, Math.Abs(a[i].g - b[i].g));
                error = Math.Max(error, Math.Abs(a[i].b - b[i].b)); error = Math.Max(error, Math.Abs(a[i].a - b[i].a));
            }
            return error;
        }
    }
}
#endif
