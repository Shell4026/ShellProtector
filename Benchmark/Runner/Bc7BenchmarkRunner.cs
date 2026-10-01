#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Shell.Protector.Tests;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Shell.Protector.Benchmark
{
    // One representative condition, rendered in preview scenes owned by this session.
    internal sealed class Bc7BenchmarkRunner : IDisposable
    {
        const int Size = 2048, Width = 1920, Height = 1080;
        const int Warmup = 60, Samples = 300, Repeats = 3, GpuDelay = 3;
        readonly string surface;
        readonly bool compareDxt1;
        readonly string[] formats;
        readonly List<Object> owned = new List<Object>();
        readonly List<RunResult> results = new List<RunResult>();
        readonly Material[] materials;
        readonly CommandBuffer[] commands, endCommands;
        readonly PreviewRenderUtility[] previews;
        readonly Recorder[] recorders;
        readonly List<long>[] gpu;
        readonly bool oldProfilerEnabled, oldGpuEnabled;
        readonly int oldVsync, oldMipLimit, oldFrameRate;
        readonly AnisotropicFiltering oldAnisotropic;
        StreamWriter raw;
        RenderTexture target;
        int repeat, frame, attempts, valid, serial, lastUnityFrame = -1;
        long submitTicks;
        bool prepared, submitted, complete, disposed;
        readonly string runtimeRoot;

        public string Output { get; }
        public string Status { get; private set; } = "Preparing textures and shaders…";

        [Serializable] sealed class RunResult
        {
            public string format;
            public int repeat, samples;
            public double medianMs, p95Ms;
        }
        [Serializable] sealed class Report
        {
            public string unity, gpu, graphicsApi, cpu, os, utc, surface, colorSpace;
            public string rendering = "In-process preview scenes; default material settings; no shader optimizer";
            public int size = Size, width = Width, height = Height, warmup = Warmup, samples = Samples, repeats = Repeats, gpuDelay = GpuDelay;
            public bool compareDxt1;
            public List<RunResult> results;
        }

        public Bc7BenchmarkRunner(string packageRoot, string surface, bool compareDxt1)
        {
            if (!SystemInfo.supportsGpuRecorder) throw new InvalidOperationException("GPU Recorder is not supported by the current graphics device.");
            this.surface = surface; this.compareDxt1 = compareDxt1;
            runtimeRoot = packageRoot.Replace('\\', '/') + "/Runtime";
            Output = Path.GetFullPath(Path.Combine("Library", "ShellProtectorBenchmark", "Results", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            formats = compareDxt1
                ? new[] { "native-bc7", "dxt1-chacha8-270", "bc7-chacha8", "native-dxt1" }
                : new[] { "native-bc7", "rgba32-chacha8-270", "bc7-chacha8" };
            materials = new Material[formats.Length];
            commands = new CommandBuffer[formats.Length]; endCommands = new CommandBuffer[formats.Length];
            previews = new PreviewRenderUtility[formats.Length]; recorders = new Recorder[formats.Length];
            gpu = formats.Select(_ => new List<long>()).ToArray();
            oldProfilerEnabled = Profiler.enabled; oldGpuEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.GPU);
            oldVsync = QualitySettings.vSyncCount; oldMipLimit = QualitySettings.globalTextureMipmapLimit;
            oldAnisotropic = QualitySettings.anisotropicFiltering; oldFrameRate = Application.targetFrameRate;
        }

        void Prepare()
        {
            Directory.CreateDirectory(Output);
            raw = new StreamWriter(Path.Combine(Output, "gpu-raw.csv"));
            raw.WriteLine("surface,size,alpha,filter,repeat,sample,format,gpu_ns,gpu_blocks,editor_update,unity_frame,cpu_submit_ns");
            Profiler.enabled = true; ProfilerDriver.SetAreaEnabled(ProfilerArea.GPU, true);
            QualitySettings.vSyncCount = 0; QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable; Application.targetFrameRate = -1;
            var source = Own(Bc7TestData.Pattern(Size, Size, !compareDxt1, true));
            source.filterMode = FilterMode.Bilinear; source.wrapMode = TextureWrapMode.Repeat;
            var comparison = Own(DecodeToRgba(source));
            if (compareDxt1)
            {
                EditorUtility.CompressTexture(comparison, TextureFormat.DXT1, TextureCompressionQuality.Fast);
                comparison.Apply(false, false);
            }
            comparison.filterMode = FilterMode.Bilinear; comparison.wrapMode = TextureWrapMode.Repeat;
            var comparisonCipher = new Chacha20(); var bc7Cipher = new Chacha20();
            var comparisonEncrypted = Encrypt(comparison, comparisonCipher);
            var bc7Encrypted = Encrypt(source, bc7Cipher);
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
                injector.Init(null, source, Bc7TestData.Key, 12, (int)ShellProtectorTextureFilter.Bilinear, runtimeRoot, bc7Cipher);
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
            materials[1] = ProtectedMaterial(comparison, comparisonEncrypted, comparisonCipher, protectedShader, injector, mip);
            materials[2] = ProtectedMaterial(source, bc7Encrypted, bc7Cipher, protectedShader, injector, mip);
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
            var images = new Color32[formats.Length][];
            for (int i = 0; i < formats.Length; ++i)
            {
                var sampler = CustomSampler.Create("ShellBenchmark." + Guid.NewGuid().ToString("N") + "." + formats[i], true);
                recorders[i] = sampler.GetRecorder(); recorders[i].enabled = true;
                if (!recorders[i].isValid) throw new InvalidOperationException("GPU Recorder is invalid: " + formats[i]);
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
                bool asynchronous = ShaderUtil.allowAsyncCompilation;
                try
                {
                    ShaderUtil.allowAsyncCompilation = false;
                    if (surface == "kernel") ShaderUtil.CompilePass(materials[i], i == 0 || i == 3 ? 0 : 2, true);
                    Submit(i);
                    images[i] = Capture(i);
                }
                finally { ShaderUtil.allowAsyncCompilation = asynchronous; }
            }
            using (var quality = new StreamWriter(Path.Combine(Output, "quality.csv")))
            {
                quality.WriteLine("format,rgb_mean_error_255,rgb_max_error_255");
                foreach (var pair in new[] { (encrypted: 1, native: compareDxt1 ? 3 : 0), (encrypted: 2, native: 0) })
                {
                    long sum = 0; int max = 0;
                    for (int p = 0; p < images[pair.native].Length; ++p)
                        for (int c = 0; c < 3; ++c)
                        {
                            int error = Math.Abs(images[pair.encrypted][p][c] - images[pair.native][p][c]);
                            sum += error; max = Math.Max(max, error);
                        }
                    double mean = sum / (Width * Height * 3.0);
                    quality.WriteLine($"{formats[pair.encrypted]},{F(mean)},{max}");
                    if (mean > 5) throw new InvalidOperationException("The benchmark image differs from the native reference: " + formats[pair.encrypted] + " (mean " + F(mean) + "/255).");
                }
            }
            prepared = true; ResetRun();
        }

        EncryptResult Encrypt(Texture2D source, Chacha20 cipher)
        {
            var encrypted = TextureEncryptManager.EncryptTexture(source, Bc7TestData.Key, cipher);
            Own(encrypted.Texture1).Apply(false, false);
            if (encrypted.Texture2 != null) Own(encrypted.Texture2).Apply(false, false);
            return encrypted;
        }

        Material ProtectedMaterial(Texture2D source, EncryptResult encrypted, Chacha20 cipher, Shader shader, Injector injector, Texture2D mip)
        {
            var material = Own(new Material(materials[0])); material.shader = shader; material.mainTexture = source;
            if (injector != null)
            {
                injector.Init(null, source, Bc7TestData.Key, 12, (int)ShellProtectorTextureFilter.Bilinear, runtimeRoot, cipher);
                injector.SetKeywords(material);
            }
            MaterialEncryptor.ConfigureDecryption(material, source, encrypted, Bc7TestData.Key, 16, 2700);
            material.SetTexture(ShaderProperties.MipTexture, mip);
            material.mainTexture = Texture2D.blackTexture;
            return material;
        }

        void Submit(int format)
        {
            var preview = previews[format];
            if (preview == null) { Graphics.ExecuteCommandBuffer(commands[format]); return; }
            float scale = EditorGUIUtility.pixelsPerPoint;
            preview.BeginPreview(new Rect(0, 0, Width / scale, Height / scale), GUIStyle.none);
            try { preview.Render(false, false); }
            finally { target = (RenderTexture)preview.EndPreview(); }
        }

        Color32[] Capture(int format)
        {
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply(false, false);
                var pixels = image.GetPixels32();
                if (pixels.Max(p => (int)p.r) - pixels.Min(p => (int)p.r) < 16)
                    throw new InvalidOperationException("Benchmark draw lacks image variation: " + formats[format]);
                string folder = Path.Combine(Output, "images"); Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, formats[format] + ".png"), image.EncodeToPNG());
                return pixels;
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }

        public bool Tick()
        {
            if (complete) return true;
            if (!prepared) { Prepare(); return false; }
            // Several editor updates can belong to one Unity frame. Collect and submit once per frame.
            if (lastUnityFrame == Time.frameCount) { EditorApplication.QueuePlayerLoopUpdate(); return false; }
            lastUnityFrame = Time.frameCount; ++serial;
            if (submitted && ++frame > Warmup + GpuDelay)
            {
                ++attempts;
                if (recorders.All(r => r.gpuSampleBlockCount == 1 && r.gpuElapsedNanoseconds > 0))
                {
                    for (int i = 0; i < formats.Length; ++i)
                    {
                        long ns = recorders[i].gpuElapsedNanoseconds; gpu[i].Add(ns);
                        raw.WriteLine($"{surface},{Size},{!compareDxt1},bilinear,{repeat},{valid},{formats[i]},{ns},1,{serial},{Time.frameCount},{submitTicks * 1000000000L / Stopwatch.Frequency}");
                    }
                    ++valid;
                }
                if (valid == Samples)
                {
                    SaveRun();
                    if (++repeat == Repeats) { complete = true; Status = "Complete"; Dispose(); return true; }
                    ResetRun();
                }
                if (attempts > 6000) throw new InvalidOperationException("Insufficient valid GPU samples. Recorder block counts: " + string.Join(",", recorders.Select(r => r.gpuSampleBlockCount)));
            }
            Status = frame <= Warmup + GpuDelay ? $"Run {repeat + 1}/{Repeats}: warming up ({frame}/{Warmup + GpuDelay})" : $"Run {repeat + 1}/{Repeats}: {valid}/{Samples} GPU samples";
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < formats.Length; ++i) Submit((i + serial + repeat) % formats.Length);
            submitTicks = Stopwatch.GetTimestamp() - start; submitted = true;
            EditorApplication.QueuePlayerLoopUpdate();
            return false;
        }

        void ResetRun() { frame = attempts = valid = 0; submitted = false; foreach (var values in gpu) values.Clear(); }

        void SaveRun()
        {
            for (int i = 0; i < formats.Length; ++i)
            {
                long[] sorted = gpu[i].OrderBy(v => v).ToArray();
                results.Add(new RunResult { format = formats[i], repeat = repeat, samples = sorted.Length,
                    medianMs = (sorted[(Samples - 1) / 2] + sorted[Samples / 2]) / 2e6,
                    p95Ms = sorted[(int)Math.Ceiling(Samples * .95) - 1] / 1e6 });
            }
            raw.Flush();
            File.WriteAllText(Path.Combine(Output, "summary.json"), JsonUtility.ToJson(new Report {
                unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceVersion,
                cpu = SystemInfo.processorType, os = SystemInfo.operatingSystem, utc = DateTime.UtcNow.ToString("O"),
                colorSpace = QualitySettings.activeColorSpace.ToString(), surface = surface, compareDxt1 = compareDxt1, results = results }, true));
        }

        static Texture2D DecodeToRgba(Texture2D source)
        {
            var linear = Bc7TestData.LinearCopy(source);
            var material = new Material(Shader.Find("Hidden/ShellProtector/BC7Test"));
            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, source.mipmapCount, false);
            try
            {
                for (int mip = 0; mip < source.mipmapCount; ++mip)
                {
                    material.SetFloat("_Lod", mip);
                    result.SetPixels32(Bc7TestData.Render(linear, material, 0, Math.Max(1, source.width >> mip), Math.Max(1, source.height >> mip)), mip);
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
        static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            raw?.Dispose();
            for (int i = 0; i < formats.Length; ++i)
            {
                if (recorders[i] != null) recorders[i].enabled = false;
                if (previews[i] != null) { previews[i].camera.RemoveAllCommandBuffers(); previews[i].Cleanup(); }
                commands[i]?.Release(); endCommands[i]?.Release();
            }
            foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
            owned.Clear(); target = null;
            QualitySettings.vSyncCount = oldVsync; QualitySettings.globalTextureMipmapLimit = oldMipLimit;
            QualitySettings.anisotropicFiltering = oldAnisotropic; Application.targetFrameRate = oldFrameRate;
            ProfilerDriver.SetAreaEnabled(ProfilerArea.GPU, oldGpuEnabled); Profiler.enabled = oldProfilerEnabled;
        }
    }
}
#endif
