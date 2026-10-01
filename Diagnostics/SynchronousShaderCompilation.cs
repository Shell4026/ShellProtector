using System;
using UnityEditor;

namespace Shell.Protector.Diagnostics
{
    // A diagnostic readback must contain the requested shader, including its first draw.
    internal sealed class SynchronousShaderCompilation : IDisposable
    {
        readonly bool previous = ShaderUtil.allowAsyncCompilation;
        internal SynchronousShaderCompilation() { ShaderUtil.allowAsyncCompilation = false; }
        public void Dispose() { ShaderUtil.allowAsyncCompilation = previous; }
    }
}
