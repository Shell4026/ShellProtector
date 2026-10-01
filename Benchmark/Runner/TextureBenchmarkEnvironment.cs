using System;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace Shell.Protector.Benchmark
{
    internal sealed class TextureBenchmarkEnvironment : IDisposable
    {
        readonly bool profiler = Profiler.enabled, gpu = ProfilerDriver.IsAreaEnabled(ProfilerArea.GPU);
        readonly int vsync = QualitySettings.vSyncCount, mipLimit = QualitySettings.globalTextureMipmapLimit;
        readonly int frameRate = Application.targetFrameRate;
        readonly AnisotropicFiltering anisotropic = QualitySettings.anisotropicFiltering;

        internal void Apply()
        {
            Profiler.enabled = true; ProfilerDriver.SetAreaEnabled(ProfilerArea.GPU, true);
            QualitySettings.vSyncCount = 0; QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable; Application.targetFrameRate = -1;
        }

        public void Dispose()
        {
            QualitySettings.vSyncCount = vsync; QualitySettings.globalTextureMipmapLimit = mipLimit;
            QualitySettings.anisotropicFiltering = anisotropic; Application.targetFrameRate = frameRate;
            ProfilerDriver.SetAreaEnabled(ProfilerArea.GPU, gpu); Profiler.enabled = profiler;
        }
    }
}
