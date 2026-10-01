using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Shell.Protector.Benchmark.TextureBenchmarkSettings;

namespace Shell.Protector.Benchmark
{
    internal sealed class TextureBenchmarkResults
    {
        internal sealed class Row
        {
            public string surface, size, alpha, filter, format;
            public int samples;
            public double medianMs, p95Ms, minRunMedianMs, maxRunMedianMs;
        }

        [Serializable] internal sealed class RunResult
        {
            public string format;
            public int repeat, samples;
            public double medianMs, p95Ms;
            [NonSerialized] internal long[] values;
        }

        readonly TextureBenchmarkSettings settings;
        readonly List<RunResult> runs = new List<RunResult>();
        internal IReadOnlyList<RunResult> Runs => runs;
        internal TextureBenchmarkResults(TextureBenchmarkSettings settings) { this.settings = settings; }

        internal void AddRun(int repeat, string format, IEnumerable<long> samples)
        {
            long[] values = samples.ToArray();
            if (values.Length != Samples || values.Any(value => value <= 0) || runs.Any(run => run.repeat == repeat && run.format == format))
                throw new InvalidDataException("GPU samples contain invalid or duplicate measurements.");
            var (median, p95) = Statistics(values);
            runs.Add(new RunResult { format = format, repeat = repeat, samples = values.Length, medianMs = median, p95Ms = p95, values = values });
        }

        internal Row[] Aggregate()
        {
            var rows = new List<Row>();
            foreach (string format in settings.Formats)
            {
                var matching = runs.Where(run => run.format == format).ToArray();
                if (matching.Length != Repeats)
                    throw new InvalidDataException($"The benchmark did not collect {Samples} valid samples in each of {Repeats} runs.");
                long[] values = matching.SelectMany(run => run.values).ToArray();
                var (median, p95) = Statistics(values);
                rows.Add(new Row { surface = settings.Surface, size = Size.ToString(), alpha = (!settings.CompareDxt1).ToString(), filter = "bilinear", format = format,
                    samples = values.Length, medianMs = median, p95Ms = p95,
                    minRunMedianMs = matching.Min(run => run.medianMs), maxRunMedianMs = matching.Max(run => run.medianMs) });
            }
            return rows.ToArray();
        }

        static (double median, double p95) Statistics(IEnumerable<long> values)
        {
            long[] sorted = values.OrderBy(value => value).ToArray();
            return ((sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2e6,
                sorted[(int)Math.Ceiling(sorted.Length * .95) - 1] / 1e6);
        }

        internal static double? AddedGpuMilliseconds(Row row, IEnumerable<Row> rows)
        {
            string native = NativeFormat(row.format);
            Row baseline = rows.FirstOrDefault(other => other.format == native && other.surface == row.surface && other.size == row.size && other.alpha == row.alpha && other.filter == row.filter);
            return baseline == null ? (double?)null : row.medianMs - baseline.medianMs;
        }
    }
}
