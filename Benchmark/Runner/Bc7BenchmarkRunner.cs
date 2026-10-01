using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using static Shell.Protector.Benchmark.TextureBenchmarkSettings;

namespace Shell.Protector.Benchmark
{
    // Advances one representative condition; preview rendering and file output have separate owners.
    internal sealed class Bc7BenchmarkRunner : IDisposable
    {
        readonly TextureBenchmarkSettings settings;
        readonly TextureBenchmarkScene scene;
        readonly TextureBenchmarkEnvironment environment;
        readonly TextureBenchmarkOutput output;
        readonly TextureBenchmarkResults results;
        readonly Recorder[] recorders;
        readonly List<long>[] gpu;
        int repeat, frame, attempts, valid, serial, lastUnityFrame = -1;
        long submitTicks;
        bool prepared, submitted, complete, disposed;

        public string Output { get; }
        public string Status { get; private set; } = "Preparing textures and shaders…";
        internal TextureBenchmarkResults.Row[] Rows { get; private set; } = Array.Empty<TextureBenchmarkResults.Row>();

        public Bc7BenchmarkRunner(string packageRoot, string surface, bool compareDxt1)
        {
            if (!SystemInfo.supportsGpuRecorder) throw new InvalidOperationException("GPU Recorder is not supported by the current graphics device.");
            settings = new TextureBenchmarkSettings(surface, compareDxt1);
            Output = Path.GetFullPath(Path.Combine("Library", "ShellProtectorBenchmark", "Results", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            scene = new TextureBenchmarkScene(packageRoot, settings);
            environment = new TextureBenchmarkEnvironment();
            output = new TextureBenchmarkOutput(Output, settings);
            results = new TextureBenchmarkResults(settings);
            recorders = new Recorder[settings.Formats.Length];
            gpu = settings.Formats.Select(_ => new List<long>()).ToArray();
        }

        void Prepare()
        {
            output.Open();
            environment.Apply();
            var samplers = new CustomSampler[recorders.Length];
            for (int i = 0; i < samplers.Length; ++i)
            {
                samplers[i] = CustomSampler.Create("ShellBenchmark." + Guid.NewGuid().ToString("N") + "." + settings.Formats[i], true);
                recorders[i] = samplers[i].GetRecorder(); recorders[i].enabled = true;
                if (!recorders[i].isValid) throw new InvalidOperationException("GPU Recorder is invalid: " + settings.Formats[i]);
            }
            output.SavePreparation(scene.Prepare(samplers));
            prepared = true; ResetRun();
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
                    for (int i = 0; i < recorders.Length; ++i)
                    {
                        long ns = recorders[i].gpuElapsedNanoseconds; gpu[i].Add(ns);
                        output.Record(repeat, valid, settings.Formats[i], ns, serial, Time.frameCount, submitTicks);
                    }
                    ++valid;
                }
                if (valid == Samples)
                {
                    for (int i = 0; i < recorders.Length; ++i) results.AddRun(repeat, settings.Formats[i], gpu[i]);
                    output.SaveRuns(results);
                    if (++repeat == Repeats)
                    {
                        Rows = results.Aggregate(); output.SaveAggregate(Rows);
                        complete = true; Status = "Complete"; Dispose(); return true;
                    }
                    ResetRun();
                }
                if (attempts > 6000) throw new InvalidOperationException("Insufficient valid GPU samples. Recorder block counts: " + string.Join(",", recorders.Select(r => r.gpuSampleBlockCount)));
            }
            Status = frame <= Warmup + GpuDelay ? $"Run {repeat + 1}/{Repeats}: warming up ({frame}/{Warmup + GpuDelay})" : $"Run {repeat + 1}/{Repeats}: {valid}/{Samples} GPU samples";
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < recorders.Length; ++i) scene.Submit((i + serial + repeat) % recorders.Length);
            submitTicks = Stopwatch.GetTimestamp() - start; submitted = true;
            EditorApplication.QueuePlayerLoopUpdate();
            return false;
        }

        void ResetRun() { frame = attempts = valid = 0; submitted = false; foreach (var values in gpu) values.Clear(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            output.Dispose();
            foreach (var recorder in recorders) if (recorder != null) recorder.enabled = false;
            scene.Dispose();
            environment.Dispose();
        }
    }
}
