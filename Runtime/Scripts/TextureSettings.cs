using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR

public class TextureSettings
{
    public static void SetRWEnableTexture(Texture2D texture)
    {
        if (texture.isReadable)
            return;
        string path = AssetDatabase.GetAssetPath(texture);
        string meta = File.ReadAllText(path + ".meta");

        meta = Regex.Replace(meta, "isReadable: 0", "isReadable: 1");
        File.WriteAllText(path + ".meta", meta);

        AssetDatabase.Refresh();
    }
    public static void SetCrunchCompression(Texture2D texture, bool crunch)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        string meta = File.ReadAllText(path + ".meta");

        if (crunch == false)
        {
            if (texture.format == TextureFormat.DXT1Crunched)
            {
                int format = 10;
                meta = Regex.Replace(meta, "textureFormat: \\d+", "textureFormat: " + format);
            }
            else if (texture.format == TextureFormat.DXT5Crunched)
            {
                int format = 12;
                meta = Regex.Replace(meta, "textureFormat: \\d+", "textureFormat: " + format);
            }
        }
        int enable = crunch ? 1 : 0;
        meta = Regex.Replace(meta, "crunchedCompression: \\d+", "crunchedCompression: " + enable);
        File.WriteAllText(path + ".meta", meta);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    public static void SetGenerateMipmap(Texture2D texture, bool generate)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        string meta = File.ReadAllText(path + ".meta");

        int enable = generate ? 1 : 0;
        meta = Regex.Replace(meta, "enableMipMap: \\d+", "enableMipMap: " + enable);
        File.WriteAllText(path + ".meta", meta);

        AssetDatabase.Refresh();
    }
}
#endif