#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shell.Protector.Benchmark;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector.Tests
{
    public class Bc7BenchmarkPreparationTests
    {
        [Test]
        public void RgbaComparison_WithAsyncCompilation_ValidatesRealImageAndPreservesSetting()
        {
            if (!SystemInfo.supportsGpuRecorder || Shader.Find(".poiyomi/Poiyomi Toon") == null)
                Assert.Ignore("Requires GPU profiling support and Poiyomi.");
            string script = AssetDatabase.FindAssets("Bc7BenchmarkRunner t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath).Single(path => path.EndsWith("/Benchmark/Runner/Bc7BenchmarkRunner.cs"));
            string packageRoot = script.Substring(0, script.LastIndexOf("/Benchmark/", StringComparison.Ordinal));
            bool previous = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = true;
                using (var runner = new Bc7BenchmarkRunner(packageRoot, ".poiyomi/Poiyomi Toon", false))
                {
                    runner.Tick(); // Preparation includes rendering and image validation, before collecting GPU times.
                    string rgba = File.ReadLines(Path.Combine(runner.Output, "quality.csv"))
                        .Single(line => line.StartsWith("rgba32-chacha8-270,"));
                    double meanError = double.Parse(rgba.Split(',')[1], CultureInfo.InvariantCulture);
                    Assert.That(meanError, Is.LessThanOrEqualTo(5), "RGBA comparison must render its image, not the compiling placeholder.");
                    Assert.That(ShaderUtil.allowAsyncCompilation, Is.True, "Preparing a benchmark must preserve the caller's compilation mode.");
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = previous; }
        }
    }
}
#endif
