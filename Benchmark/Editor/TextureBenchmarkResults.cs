#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Shell.Protector.Benchmark
{
    internal static class TextureBenchmarkResults
    {
        internal sealed class Row
        {
            public string surface, size, alpha, filter, format;
            public int samples;
            public double medianMs, p95Ms, minRunMedianMs, maxRunMedianMs;
        }

        internal static Row[] ReadAndSave(string directory)
        {
            var samples = new Dictionary<string, Dictionary<int, List<long>>>();
            var ids = new HashSet<string>();
            foreach (string line in File.ReadLines(Path.Combine(directory, "gpu-raw.csv")).Skip(1))
            {
                string[] fields = line.Split(',');
                string condition = string.Join(",", fields.Take(4)) + "," + fields[6];
                int repeat = int.Parse(fields[4], CultureInfo.InvariantCulture);
                long ns = long.Parse(fields[7], CultureInfo.InvariantCulture);
                if (fields[8] != "1" || ns <= 0 || !ids.Add(condition + "," + repeat + "," + fields[5]))
                    throw new InvalidDataException("GPU samples contain invalid or duplicate measurements.");
                if (!samples.TryGetValue(condition, out var runs)) samples.Add(condition, runs = new Dictionary<int, List<long>>());
                if (!runs.TryGetValue(repeat, out var values)) runs.Add(repeat, values = new List<long>());
                values.Add(ns);
            }
            if (samples.Count == 0) throw new InvalidDataException("No GPU samples were collected.");
            var rows = new List<Row>();
            foreach (var sample in samples)
            {
                if (sample.Value.Count != 3 || sample.Value.Values.Any(run => run.Count != 300))
                    throw new InvalidDataException("The benchmark did not collect 300 valid samples in each of three runs.");
                long[] times = sample.Value.Values.SelectMany(run => run).OrderBy(v => v).ToArray();
                double[] medians = sample.Value.Values.Select(run => Median(run.OrderBy(v => v).ToArray())).ToArray();
                string[] fields = sample.Key.Split(',');
                rows.Add(new Row { surface = fields[0], size = fields[1], alpha = fields[2], filter = fields[3], format = fields[4],
                    samples = times.Length, medianMs = Median(times) / 1e6, p95Ms = times[(int)Math.Ceiling(times.Length * .95) - 1] / 1e6,
                    minRunMedianMs = medians.Min() / 1e6, maxRunMedianMs = medians.Max() / 1e6 });
            }
            using (var csv = new StreamWriter(Path.Combine(directory, "aggregate.csv")))
            {
                csv.WriteLine("surface,size,alpha,filter,format,samples,median_ms,p95_ms,run_median_min_ms,run_median_max_ms,added_gpu_ms");
                foreach (Row row in rows)
                    csv.WriteLine(string.Join(",", row.surface, row.size, row.alpha, row.filter, row.format, row.samples,
                        F(row.medianMs), F(row.p95Ms), F(row.minRunMedianMs), F(row.maxRunMedianMs), AddedGpuMilliseconds(row, rows) is double added ? F(added) : ""));
            }
            return rows.ToArray();
        }

        internal static double? AddedGpuMilliseconds(Row row, IEnumerable<Row> rows)
        {
            string native = row.format == "bc7-chacha8" ? "native-bc7" : row.format == "dxt1-chacha8-270" ? "native-dxt1" : null;
            Row baseline = rows.FirstOrDefault(other => other.format == native && other.surface == row.surface && other.size == row.size && other.alpha == row.alpha && other.filter == row.filter);
            return baseline == null ? (double?)null : row.medianMs - baseline.medianMs;
        }

        static double Median(long[] sorted) => (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2.0;
        static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
#endif
