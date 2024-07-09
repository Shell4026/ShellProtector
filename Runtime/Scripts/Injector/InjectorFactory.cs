#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
    public class InjectorFactory
    {
        public static Injector GetInjector(Shader shader)
        {
            AssetManager shader_manager = AssetManager.GetInstance();
            if (shader_manager.IsPoiyomi(shader))
                return new PoiyomiInjector();
            else if (shader_manager.IslilToon(shader))
                return new LilToonInjector();
            else
                return null;
        }
    }
}
#endif