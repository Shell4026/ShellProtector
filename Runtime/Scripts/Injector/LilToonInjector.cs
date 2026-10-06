#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    public class LilToonInjector : Injector
    {
        public override bool CanHandle(Shader shader)
        {
            return ShaderManager.IsLilToon(shader);
        }

        // Every lilToon material uses the project's copy of the custom shader (LilToonShaders); nothing is injected.
        protected override Shader CustomInject(Material mat, string decodeDir, string outputPath, Texture2D tex, bool hasLimTexture = false, bool hasLimTexture2 = false, bool outlineTex = false)
        {
            string shaderName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mat.shader));
            return LilToonShaders.GetShader(AssetDir, shaderName);
        }
    }
}
#endif
