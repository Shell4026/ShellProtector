#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Shell.Protector
{
    [InitializeOnLoad]
    public class CompileErrorListener
    {
        static CompileErrorListener()
        {
            CompilationPipeline.assemblyCompilationFinished += (string s, CompilerMessage[] message) =>
            {
                foreach(var m in message)
                {
                    if(m.type == CompilerMessageType.Error)
                    {
                        if(!m.message.Contains("The type or namespace name 'lilToonInspector' could not be found"))
                            continue;
                        if (!m.message.Contains("The type or namespace name 'Thry' could not be found"))
                            continue;
                        ShaderManager.GetInstance().ResetDefine();
                        break;
                    }
                }
                
            };
        }
    }
}
#endif