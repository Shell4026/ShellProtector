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
            Injector[] adapters =
            {
                new PoiyomiInjector(),
                new LilToonInjector()
            };

            foreach (Injector adapter in adapters)
            {
                if (adapter.CanHandle(shader))
                    return adapter;
            }

            return null;
        }
    }
}
#endif
