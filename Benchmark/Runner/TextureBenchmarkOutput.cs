using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using static Shell.Protector.Benchmark.TextureBenchmarkSettings;

namespace Shell.Protector.Benchmark
{
    internal sealed class TextureBenchmarkOutput : IDisposable
    {
        readonly string directory;
        readonly TextureBenchmarkSettings settings;
        StreamWriter raw;

        [Serializable] sealed class Report
        {
            public string unity, gpu, graphicsApi, cpu, os, utc, surface, colorSpace;
            public string rendering = "In-process preview scenes; default material settings; no shader optimizer";
            public int size = Size, width = Width, height = Height, warmup = Warmup, samples = Samples, repeats = Repeats, gpuDelay = GpuDelay;
            public bool compareDxt1;
            public List<TextureBenchmarkResults.RunResult> results;
        }

        internal TextureBenchmarkOutput(string directory, TextureBenchmarkSettings settings)
        {
            this.directory = directory; this.settings = settings;
        }

        internal void Open()
        {
            Directory.CreateDirectory(directory);
            raw = new StreamWriter(Path.Combine(directory, "gpu-raw.csv"));
            raw.WriteLine("surface,size,alpha,filter,repeat,sample,format,gpu_ns,gpu_blocks,editor_update,unity_frame,cpu_submit_ns");
        }

        internal void Record(int repeat, int sample, string format, long nanoseconds, int update, int frame, long submitTicks)
        {
            raw.WriteLine($"{settings.Surface},{Size},{!settings.CompareDxt1},bilinear,{repeat},{sample},{format},{nanoseconds},1,{update},{frame},{submitTicks * 1000000000L / Stopwatch.Frequency}");
        }

        internal void SaveRuns(TextureBenchmarkResults results)
        {
            raw.Flush();
            File.WriteAllText(Path.Combine(directory, "summary.json"), JsonUtility.ToJson(new Report {
                unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceVersion,
                cpu = SystemInfo.processorType, os = SystemInfo.operatingSystem, utc = DateTime.UtcNow.ToString("O"),
                colorSpace = QualitySettings.activeColorSpace.ToString(), surface = settings.Surface,
                compareDxt1 = settings.CompareDxt1, results = results.Runs.ToList() }, true));
        }

        internal void SaveAggregate(TextureBenchmarkResults.Row[] rows)
        {
            using (var csv = new StreamWriter(Path.Combine(directory, "aggregate.csv")))
            {
                csv.WriteLine("surface,size,alpha,filter,format,samples,median_ms,p95_ms,run_median_min_ms,run_median_max_ms,added_gpu_ms");
                foreach (var row in rows)
                    csv.WriteLine(string.Join(",", row.surface, row.size, row.alpha, row.filter, row.format, row.samples,
                        F(row.medianMs), F(row.p95Ms), F(row.minRunMedianMs), F(row.maxRunMedianMs), TextureBenchmarkResults.AddedGpuMilliseconds(row, rows) is double added ? F(added) : ""));
            }
        }

        internal void SavePreparation(BenchmarkImage[] images)
        {
            string folder = Path.Combine(directory, "images");
            for (int i = 0; i < images.Length; ++i)
            {
                var pixels = images[i].Pixels;
                if (pixels.Max(p => (int)p.r) - pixels.Min(p => (int)p.r) < 16)
                    throw new InvalidOperationException("Benchmark draw lacks image variation: " + settings.Formats[i]);
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, settings.Formats[i] + ".png"), images[i].Png);
            }
            using (var quality = new StreamWriter(Path.Combine(directory, "quality.csv")))
            {
                quality.WriteLine("format,rgb_mean_error_255,rgb_max_error_255");
                foreach (var pair in new[] { (encrypted: 1, native: settings.CompareDxt1 ? 3 : 0), (encrypted: 2, native: 0) })
                {
                    long sum = 0; int max = 0;
                    for (int p = 0; p < images[pair.native].Pixels.Length; ++p)
                        for (int c = 0; c < 3; ++c)
                        {
                            int error = Math.Abs(images[pair.encrypted].Pixels[p][c] - images[pair.native].Pixels[p][c]);
                            sum += error; max = Math.Max(max, error);
                        }
                    double mean = sum / (Width * Height * 3.0);
                    quality.WriteLine($"{settings.Formats[pair.encrypted]},{F(mean)},{max}");
                    if (mean > 5) throw new InvalidOperationException("The benchmark image differs from the native reference: " + settings.Formats[pair.encrypted] + " (mean " + F(mean) + "/255).");
                }
            }
        }

        static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        public void Dispose() => raw?.Dispose();
    }
}
