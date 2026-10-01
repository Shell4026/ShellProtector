using UnityEngine;

namespace Shell.Protector
{
    internal static class TextureMipUtility
    {
        // Preserve the legacy one-row and mip-tail behavior used by RGB and DXT storage.
        internal static int LegacyLevel(int width, int height)
        {
            if (width < 1 || height <= 1) return 0;
            return Mathf.Max((int)Mathf.Log(width, 2), (int)Mathf.Log(height, 2));
        }

        internal static int FullCount(int width, int height) => 1 + (int)Mathf.Log(Mathf.Max(width, height), 2);

        internal static (int, int) Offsets(Texture2D texture, int blockShift = 0) =>
            (12 - (int)Mathf.Log(texture.width, 2) + blockShift, 12 - (int)Mathf.Log(texture.height, 2) + blockShift);
    }
}
