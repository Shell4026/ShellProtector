#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shell.Protector.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using static Shell.Protector.Benchmark.TextureBenchmarkSettings;

namespace Shell.Protector.Benchmark
{
    internal readonly struct BenchmarkImage
    {
        internal readonly Color32[] Pixels;
        internal readonly byte[] Png;
        internal BenchmarkImage(Color32[] pixels, byte[] png) { Pixels = pixels; Png = png; }
    }

    // Owns inputs and preview rendering; it does not collect timings or save results.
    internal sealed class TextureBenchmarkScene : IDisposable
    {
        readonly string surface, runtimeRoot;
        readonly bool compareDxt1;
        readonly string[] formats;
        readonly List<Object> owned = new List<Object>();
        readonly Material[] materials;
        readonly CommandBuffer[] commands, endCommands;
        readonly PreviewRenderUtility[] previews;
        RenderTexture target;

        internal TextureBenchmarkScene(string packageRoot, TextureBenchmarkSettings settings)
        {
            runtimeRoot = OutputPaths.Normalize(packageRoot) + "/Runtime";
            surface = settings.Surface; compareDxt1 = settings.CompareDxt1; formats = settings.Formats;
            materials = new Material[formats.Length];
            commands = new CommandBuffer[formats.Length]; endCommands = new CommandBuffer[formats.Length];
            previews = new PreviewRenderUtility[formats.Length];
        }

        internal BenchmarkImage[] Prepare(CustomSampler[] samplers)
        {
            var source = Own(TextureDiagnostics.Pattern(Size, Size, !compareDxt1, true));
            source.filterMode = FilterMode.Bilinear; source.wrapMode = TextureWrapMode.Repeat;
            var comparison = Own(DecodeToRgba(source));
            if (compareDxt1)
            {
                EditorUtility.CompressTexture(comparison, TextureFormat.DXT1, TextureCompressionQuality.Fast);
                comparison.Apply(false, false);
            }
            comparison.filterMode = FilterMode.Bilinear; comparison.wrapMode = TextureWrapMode.Repeat;
            var comparisonEncrypted = Encrypt(comparison);
            var bc7Encrypted = Encrypt(source);
            var mip = Own(surface == "kernel" ? new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                : TextureEncryptManager.GenerateRefMipmap(Size, Size, false, true));
            if (surface == "kernel") { mip.SetPixel(0, 0, Color.black); mip.Apply(false, false); }
            Shader nativeShader = Shader.Find(surface == "kernel" ? "Hidden/ShellProtector/BC7Test" : surface);
            if (nativeShader == null) throw new InvalidOperationException("Shader is not installed: " + surface);
            materials[0] = Own(new Material(nativeShader)); materials[0].mainTexture = source;
            Shader protectedShader = nativeShader;
            Injector injector = null;
            if (surface != "kernel")
            {
                injector = InjectorFactory.GetInjector(nativeShader);
                if (injector == null) throw new InvalidOperationException("Unsupported benchmark shader: " + surface);
                injector.Init(null, source, TextureDiagnostics.Key, 12, (int)ShellProtectorTextureFilter.Bilinear, runtimeRoot);
                if (injector is PoiyomiInjector poiyomi)
                {
                    string sourceCode = File.ReadAllText(AssetDatabase.GetAssetPath(nativeShader));
                    string code = poiyomi.BuildShaderSource(nativeShader, sourceCode, runtimeRoot + "/Shader/Protector.cginc");
                    if (code == null) throw new InvalidOperationException("Poiyomi shader injection failed.");
                    protectedShader = Own(ShaderUtil.CreateShaderAsset(code, false));
                }
                else protectedShader = injector.Inject(materials[0], runtimeRoot + "/Shader/Protector.cginc", "", source);
                if (protectedShader == null) throw new InvalidOperationException("Benchmark shader injection failed.");
            }
            materials[1] = ProtectedMaterial(comparison, comparisonEncrypted, protectedShader, injector, mip);
            materials[2] = ProtectedMaterial(source, bc7Encrypted, protectedShader, injector, mip);
            if (compareDxt1) { materials[3] = Own(new Material(materials[0])); materials[3].mainTexture = comparison; }
            foreach (var material in materials)
            {
                if (surface == "kernel")
                {
                    material.SetTexture("_MipTex", mip);
                    material.SetVector("_UVTransform", new Vector4(1.17f, .93f, -.031f, .023f));
                }
                else { material.mainTextureScale = new Vector2(1.17f, .93f); material.mainTextureOffset = new Vector2(-.031f, .023f); }
            }
            if (surface == "kernel")
            {
                target = Own(new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
                target.Create();
            }
            Mesh quad = surface == "kernel" ? null : Own(CreateQuad());
            var images = new BenchmarkImage[formats.Length];
            for (int i = 0; i < formats.Length; ++i)
            {
                var sampler = samplers[i];
                commands[i] = new CommandBuffer { name = formats[i] };
                if (surface == "kernel")
                {
                    commands[i].SetRenderTarget(target); commands[i].ClearRenderTarget(true, true, Color.black);
                    commands[i].BeginSample(sampler);
                    bool native = i == 0 || i == 3;
                    commands[i].Blit(i == 3 ? comparison : source, target, materials[i], native ? 0 : 2);
                    commands[i].EndSample(sampler);
                }
                else
                {
                    var preview = previews[i] = new PreviewRenderUtility();
                    preview.ambientColor = Color.white;
                    preview.lights[0].color = Color.white; preview.lights[0].intensity = 1;
                    preview.lights[0].transform.rotation = Quaternion.identity; preview.lights[1].intensity = 0;
                    var subject = EditorUtility.CreateGameObjectWithHideFlags("BenchmarkQuad", HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
                    preview.AddSingleGO(subject);
                    subject.GetComponent<MeshFilter>().sharedMesh = quad;
                    subject.GetComponent<MeshRenderer>().sharedMaterial = materials[i];
                    subject.transform.localScale = new Vector3((float)Width / Height, 1, 1);
                    Camera camera = preview.camera;
                    camera.transform.position = new Vector3(0, 0, -2);
                    camera.orthographic = true; camera.orthographicSize = .5f;
                    camera.nearClipPlane = .1f; camera.farClipPlane = 10;
                    camera.allowHDR = false; camera.allowMSAA = false;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                    commands[i].BeginSample(sampler);
                    endCommands[i] = new CommandBuffer { name = formats[i] + ".end" }; endCommands[i].EndSample(sampler);
                    camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commands[i]);
                    camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, endCommands[i]);
                }
                // Interactive editors may render the cyan compiling placeholder on the first draw.
                // Finish compilation before validating the image or starting the warmup count.
                using (new SynchronousShaderCompilation())
                {
                    if (surface == "kernel") ShaderUtil.CompilePass(materials[i], i == 0 || i == 3 ? 0 : 2, true);
                    Submit(i);
                    images[i] = Capture();
                }
            }
            return images;
        }

        EncryptResult Encrypt(Texture2D source)
        {
            var cipher = TextureEncryptManager.CreateCipher(source, ShellProtectorAlgorithm.Chacha, 0);
            var encrypted = TextureEncryptManager.EncryptTexture(source, TextureDiagnostics.Key, cipher);
            Own(encrypted.Texture1).Apply(false, false);
            if (encrypted.Texture2 != null) Own(encrypted.Texture2).Apply(false, false);
            return encrypted;
        }

        Material ProtectedMaterial(Texture2D source, EncryptResult encrypted, Shader shader, Injector injector, Texture2D mip)
        {
            var material = Own(new Material(materials[0])); material.shader = shader; material.mainTexture = source;
            if (injector != null)
            {
                injector.Init(null, source, TextureDiagnostics.Key, 12, (int)ShellProtectorTextureFilter.Bilinear, runtimeRoot);
                injector.SetKeywords(material);
            }
            MaterialEncryptor.ConfigureDecryption(material, source, encrypted, TextureDiagnostics.Key, 16, 2700);
            material.SetTexture(ShaderProperties.MipTexture, mip);
            material.mainTexture = Texture2D.blackTexture;
            return material;
        }

        internal void Submit(int format)
        {
            var preview = previews[format];
            if (preview == null) { Graphics.ExecuteCommandBuffer(commands[format]); return; }
            float scale = EditorGUIUtility.pixelsPerPoint;
            preview.BeginPreview(new Rect(0, 0, Width / scale, Height / scale), GUIStyle.none);
            try { preview.Render(false, false); }
            finally { target = (RenderTexture)preview.EndPreview(); }
        }

        BenchmarkImage Capture()
        {
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply(false, false);
                var pixels = image.GetPixels32();
                return new BenchmarkImage(pixels, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }

        static Texture2D DecodeToRgba(Texture2D source)
        {
            var linear = TextureDiagnostics.LinearCopy(source);
            var material = new Material(Shader.Find("Hidden/ShellProtector/BC7Test"));
            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, source.mipmapCount, false);
            try
            {
                for (int mip = 0; mip < source.mipmapCount; ++mip)
                {
                    material.SetFloat("_Lod", mip);
                    result.SetPixels32(TextureDiagnostics.Render(linear, material, 0, Math.Max(1, source.width >> mip), Math.Max(1, source.height >> mip)), mip);
                }
                result.Apply(false, false); return result;
            }
            catch { Object.DestroyImmediate(result); throw; }
            finally { Object.DestroyImmediate(linear); Object.DestroyImmediate(material); }
        }

        static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "BenchmarkQuad" };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.normals = Enumerable.Repeat(Vector3.back, 4).ToArray();
            mesh.tangents = Enumerable.Repeat(new Vector4(1,0,0,-1), 4).ToArray();
            mesh.colors = Enumerable.Repeat(Color.white, 4).ToArray();
            mesh.triangles = new[] { 0,2,1,2,3,1 }; mesh.RecalculateBounds();
            return mesh;
        }

        T Own<T>(T value) where T : Object { value.hideFlags = HideFlags.HideAndDontSave; owned.Add(value); return value; }
        public void Dispose()
        {
            for (int i = 0; i < formats.Length; ++i)
            {
                if (previews[i] != null) { previews[i].camera.RemoveAllCommandBuffers(); previews[i].Cleanup(); }
                commands[i]?.Release(); endCommands[i]?.Release();
            }
            foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
            owned.Clear(); target = null;
        }
    }
}
#endif
