using System.IO;
using System.Linq;
using NUnit.Framework;
using Shell.Protector.Benchmark;

namespace Shell.Protector.Tests
{
    public class TextureBenchmarkResultsTests
    {
        [Test]
        public void ReportsPooledTimingsRunVariationAndMatchingNativeOverhead()
        {
            var settings = new TextureBenchmarkSettings("kernel", true);
            var results = new TextureBenchmarkResults(settings);
            for (int repeat = 0; repeat < TextureBenchmarkSettings.Repeats; ++repeat)
                foreach (string format in settings.Formats)
                {
                    long offset = format == TextureBenchmarkSettings.Dxt1 ? 2000000 : format == TextureBenchmarkSettings.NativeDxt1 ? 1000000 : 0;
                    results.AddRun(repeat, format, Enumerable.Range(1, TextureBenchmarkSettings.Samples).Select(i => offset + (repeat * 300L + i) * 1000));
                }
            var rows = results.Aggregate();
            var dxt = rows.Single(row => row.format == TextureBenchmarkSettings.Dxt1);
            Assert.That(dxt.samples, Is.EqualTo(900));
            Assert.That(dxt.medianMs, Is.EqualTo(2.4505).Within(1e-9));
            Assert.That(dxt.p95Ms, Is.EqualTo(2.855).Within(1e-9));
            Assert.That(dxt.minRunMedianMs, Is.EqualTo(2.1505).Within(1e-9));
            Assert.That(dxt.maxRunMedianMs, Is.EqualTo(2.7505).Within(1e-9));
            Assert.That(TextureBenchmarkResults.AddedGpuMilliseconds(dxt, rows), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void RejectsDuplicateRunMeasurements()
        {
            var results = new TextureBenchmarkResults(new TextureBenchmarkSettings("kernel", false));
            var values = Enumerable.Repeat(1000L, TextureBenchmarkSettings.Samples);
            results.AddRun(0, TextureBenchmarkSettings.Bc7, values);
            Assert.Throws<InvalidDataException>(() => results.AddRun(0, TextureBenchmarkSettings.Bc7, values));
        }

        [Test]
        public void DoesNotReportAnIncompleteComparison()
        {
            var results = new TextureBenchmarkResults(new TextureBenchmarkSettings("kernel", false));
            Assert.Throws<InvalidDataException>(() => results.Aggregate());
        }
    }
}
