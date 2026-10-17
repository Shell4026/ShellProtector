#if UNITY_EDITOR
using System.Linq;
using UnityEngine;

namespace Shell.Protector
{
    // What the NDMF build's Transforming pass leaves for its Optimizing pass (NdmfPlugin), which obfuscates the
    // blendshapes after the tools that patch or optimize them by their original names: FermataFT and Avatar Optimizer.
    public sealed class DeferredObfuscation
    {
        readonly BuildSettings settings;
        // Taken while the renderers still exist: a renderer that is gone by the Optimizing phase has no name anymore.
        readonly string[] rendererNames;

        public DeferredObfuscation(BuildSettings settings)
        {
            this.settings = settings;
            settings.ObfuscationRenderers.RemoveAll(r => r == null);
            rendererNames = settings.ObfuscationRenderers.Select(r => r.name).ToArray();
        }

        public void Run(GameObject avatar)
        {
            // Avatar Optimizer's automatic mesh merging replaces the renderers it merges, and their blendshapes would
            // silently keep their names.
            for (int i = 0; i < rendererNames.Length; ++i)
            {
                if (settings.ObfuscationRenderers[i] == null)
                    Debug.LogWarningFormat("[ShellProtector] {0} was removed during the build before its blendshapes were obfuscated, so they keep their names. If Avatar Optimizer merged it, exclude it in Trace and Optimize.", rendererNames[i]);
            }
            Pipeline.ObfuscateBlendShapesInPlace(avatar, settings);
        }
    }
}
#endif
