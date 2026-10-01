namespace Shell.Protector.Benchmark
{
    internal sealed class TextureBenchmarkSettings
    {
        internal const int Size = 2048, Width = 1920, Height = 1080;
        internal const int Warmup = 60, Samples = 300, Repeats = 3, GpuDelay = 3;
        internal const string NativeBc7 = "native-bc7", NativeDxt1 = "native-dxt1";
        internal const string Bc7 = "bc7-chacha8", Dxt1 = "dxt1-chacha8-270", Rgba = "rgba32-chacha8-270";
        internal readonly string Surface;
        internal readonly bool CompareDxt1;
        internal readonly string[] Formats;

        internal TextureBenchmarkSettings(string surface, bool compareDxt1)
        {
            Surface = surface; CompareDxt1 = compareDxt1;
            Formats = compareDxt1 ? new[] { NativeBc7, Dxt1, Bc7, NativeDxt1 } : new[] { NativeBc7, Rgba, Bc7 };
        }

        internal static string NativeFormat(string format) => format == Bc7 ? NativeBc7 : format == Dxt1 ? NativeDxt1 : null;
    }
}
