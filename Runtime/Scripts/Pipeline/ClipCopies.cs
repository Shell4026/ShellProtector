#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
    // The clips a build copied before changing them. A clip is copied once, and its copy is changed in place by the later
    // steps, so the blendshape obfuscation and the material swap rewrite never copy each other's copies.
    public sealed class ClipCopies
    {
        readonly Dictionary<AnimationClip, AnimationClip> copies = new Dictionary<AnimationClip, AnimationClip>(); // original, copy
        readonly HashSet<AnimationClip> created = new HashSet<AnimationClip>();

        // The clip to change instead of this one: a copy saved in the output folder, or the clip itself if this build
        // created it.
        public AnimationClip GetWritable(AnimationClip clip, string suffix, OutputPaths paths, AssetWriter writer)
        {
            if (created.Contains(clip))
                return clip;
            if (copies.TryGetValue(clip, out AnimationClip copy))
                return copy;

            copy = Object.Instantiate(clip);
            copy.name = clip.name;
            writer.CreateAssetInFolder(copy, paths.Folders.AnimGuid, paths.AnimationClipName(clip, suffix));
            copies.Add(clip, copy);
            created.Add(copy);
            return copy;
        }
    }
}
#endif
