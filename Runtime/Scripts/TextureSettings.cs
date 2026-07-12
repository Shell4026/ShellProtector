#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    public struct TextureImportChange
    {
        public bool changed;
        public bool readWriteChanged;
        public bool crunchChanged;
        public bool mipmapChanged;
    }

    public class TextureSettings
    {
        public static TextureImportChange SetRWEnableTexture(Texture2D texture)
        {
            return Apply(texture, readable: true, crunch: null, generateMipmaps: null);
        }

        public static TextureImportChange SetCrunchCompression(Texture2D texture, bool crunch)
        {
            return Apply(texture, readable: null, crunch: crunch, generateMipmaps: null);
        }

        public static TextureImportChange SetGenerateMipmap(Texture2D texture, bool generate)
        {
            return Apply(texture, readable: null, crunch: null, generateMipmaps: generate);
        }

        static TextureImportChange Apply(Texture2D texture, bool? readable, bool? crunch, bool? generateMipmaps)
        {
            TextureImportChange result = new TextureImportChange();
            if (texture == null)
                return result;

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
                return result;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return result;

            if (readable.HasValue && importer.isReadable != readable.Value)
            {
                importer.isReadable = readable.Value;
                result.readWriteChanged = true;
            }

            if (crunch.HasValue && importer.crunchedCompression != crunch.Value)
            {
                importer.crunchedCompression = crunch.Value;
                result.crunchChanged = true;
            }

            if (generateMipmaps.HasValue && importer.mipmapEnabled != generateMipmaps.Value)
            {
                importer.mipmapEnabled = generateMipmaps.Value;
                result.mipmapChanged = true;
            }

            result.changed = result.readWriteChanged || result.crunchChanged || result.mipmapChanged;
            if (result.changed)
                importer.SaveAndReimport();

            return result;
        }
    }
}
#endif
