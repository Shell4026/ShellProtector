#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Shell.Protector.Tests.Unit
{
    // The bug report lives in the editor assembly, which this one doesn't reference, so it is called by name.
    public class BugReportTests
    {
        [Test]
        public void NdmfPasses_ListShellProtectorsPassesInTheirPhases()
        {
            Assume.That(Type.GetType("nadena.dev.ndmf.PluginResolver, nadena.dev.ndmf"), Is.Not.Null, "NDMF isn't installed.");
            Type bugReport = Type.GetType("Shell.Protector.BugReport, shell.protector.Editor");
            Assert.That(bugReport, Is.Not.Null);
            MethodInfo append = bugReport.GetMethod("AppendNdmfPasses", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(append, Is.Not.Null);

            var report = new StringBuilder();
            append.Invoke(null, new object[] { report });
            Debug.Log(report);
            string[] lines = report.ToString().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

            int transforming = Array.IndexOf(lines, "Transforming:");
            int optimizing = Array.IndexOf(lines, "Optimizing:");
            int encrypt = Array.FindIndex(lines, l => l.Contains("NdmfPlugin (ShellProtector): Encrypting2"));
            int obfuscate = Array.FindIndex(lines, l => l.Contains("NdmfPlugin (ShellProtector): Obfuscating blendshapes"));
            Assert.That(transforming, Is.GreaterThanOrEqualTo(0));
            Assert.That(encrypt, Is.GreaterThan(transforming));
            Assert.That(optimizing, Is.GreaterThan(encrypt));
            Assert.That(obfuscate, Is.GreaterThan(optimizing));
        }
    }
}
#endif
