#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Shell.Protector
{
    [InitializeOnLoad]
    public class CompileErrorListener
    {
        private static int retryLimit = 3;
        private static int retryCount = 0;

        static CompileErrorListener()
        {
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        }

        private static void OnAssemblyCompilationFinished(string assembly, CompilerMessage[] messages)
        {
            bool foundTargetError = false;

            foreach (var m in messages)
            {
                if (m.type != CompilerMessageType.Error)
                    continue;

                if (
                    m.message.Contains("The type or namespace name 'lilToonInspector' could not be found") ||
                    m.message.Contains("The type or namespace name 'Thry' could not be found") ||
                    m.message.Contains("The name 'ShaderOptimizer' does not exist in the current context")
                )
                {
                    foundTargetError = true;
                    break;
                }
            }

            if (foundTargetError)
            {
                retryCount++;

                if (retryCount <= retryLimit)
                {
                    Debug.LogErrorFormat("Shader check error -> ResetDefine {0}/{0}", retryCount, retryLimit);
                    AssetManager.GetInstance().ResetDefine();
                }
                else
                {
                    Debug.LogError("Failed ResetDefine");
                }
            }
            else
            {
                retryCount = 0;
            }
        }
    }
}
#endif
