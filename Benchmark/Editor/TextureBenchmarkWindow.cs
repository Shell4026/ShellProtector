#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector.Benchmark
{
    public sealed class TextureBenchmarkWindow : EditorWindow
    {
        static readonly string[] Comparisons = { "BC7 / RGBA32", "BC7 / DXT1" };
        static readonly string[] SurfaceNames = { "Poiyomi", "lilToon", "Decoder only" };
        static readonly string[] Surfaces = { ".poiyomi/Poiyomi Toon", "lilToon", "kernel" };
        [SerializeField] int comparison, surface;
        [SerializeField] string lastOutput;
        Bc7BenchmarkRunner job;
        TextureBenchmarkResults.Row[] rows = Array.Empty<TextureBenchmarkResults.Row>();
        string status = "Ready";
        Vector2 scroll;

        [MenuItem("Tools/ShellProtector/Texture Benchmark")]
        public static void Open() => GetWindow<TextureBenchmarkWindow>("Texture Benchmark");

        void OnEnable()
        {
            minSize = new Vector2(540, 360);
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        void OnDisable()
        {
            Stop();
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.quitting -= Stop;
        }

        void OnGUI()
        {
            bool running = job != null;
            using (new EditorGUI.DisabledScope(running))
            {
                comparison = EditorGUILayout.Popup("Compare", comparison, Comparisons);
                surface = EditorGUILayout.Popup("Shader", surface, SurfaceNames);
            }
            EditorGUILayout.LabelField("Texture", comparison == 0 ? "2K, alpha pattern, Bilinear" : "2K, opaque pattern, Bilinear");
            EditorGUILayout.LabelField("Measurement", "60 warmup frames, 300 GPU samples × 3 runs");
            EditorGUILayout.HelpBox("Runs in this editor using temporary preview scenes. Your open scenes are preserved, and profiling settings are restored when the run ends.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(running || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Run benchmark")) StartBenchmark();
                using (new EditorGUI.DisabledScope(!running))
                    if (GUILayout.Button("Cancel")) Stop();
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(lastOutput) || !Directory.Exists(lastOutput)))
                    if (GUILayout.Button("Open results")) EditorUtility.RevealInFinder(lastOutput);
            }
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (rows.Length > 0)
            {
                EditorGUILayout.Space();
                DrawRow("Format", "Median (ms)", "p95 (ms)", "Added (ms)");
                foreach (var row in rows)
                {
                    double? added = TextureBenchmarkResults.AddedGpuMilliseconds(row, rows);
                    DrawRow(row.format, row.medianMs.ToString("F3"), row.p95Ms.ToString("F3"), added?.ToString("F3") ?? "—");
                }
                EditorGUILayout.HelpBox("Times cover the rendered draw at 1920×1080. Added time is the difference from that format's native median. Raw samples and run statistics are saved with the results.", MessageType.None);
            }
            EditorGUILayout.EndScrollView();
        }

        static void DrawRow(string name, string median, string p95, string added)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(name, GUILayout.MinWidth(180));
                GUILayout.Label(median, GUILayout.Width(95));
                GUILayout.Label(p95, GUILayout.Width(85));
                GUILayout.Label(added, GUILayout.Width(85));
            }
        }

        void StartBenchmark()
        {
            try
            {
                string script = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                string packageRoot = script.Substring(0, script.LastIndexOf("/Benchmark/", StringComparison.Ordinal));
                job = new Bc7BenchmarkRunner(packageRoot, Surfaces[surface], comparison == 1);
                rows = Array.Empty<TextureBenchmarkResults.Row>();
                lastOutput = job.Output;
                status = job.Status;
            }
            catch (Exception e) { status = e.Message; Debug.LogException(e); }
        }

        void Tick()
        {
            if (job == null) return;
            try
            {
                if (job.Tick())
                {
                    rows = TextureBenchmarkResults.ReadAndSave(lastOutput);
                    status = "Complete";
                    job.Dispose(); job = null;
                }
                else status = job.Status;
            }
            catch (Exception e)
            {
                Stop(); status = e.Message; Debug.LogException(e);
            }
            Repaint();
        }

        void Stop()
        {
            if (job == null) return;
            job.Dispose(); job = null;
            status = "Cancelled";
            Repaint();
        }
    }
}
#endif
