#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Shell.Protector
{
    public class AnimatorManager : ScriptableObject
    {
        // Copies each AnimatorController, with its own copy of every blend tree it uses. Override controllers aren't
        // edited by the build, so their entries stay null. Returns null if a controller can't be copied: the build
        // would then edit the original.
        public static AnimatorController[] DuplicateAnimators(RuntimeAnimatorController[] animators, OutputPaths paths, AssetWriter writer)
        {
            var targetPaths = new Dictionary<string, string>();
            var duplicatedAnimators = new AnimatorController[animators.Length];
            var copiedBlendTrees = new HashSet<AnimatorController>();

            for (int i = 0; i < animators.Length; ++i)
            {
                if (!(animators[i] is AnimatorController animator))
                    continue;

                string sourcePath = AssetDatabase.GetAssetPath(animator);
                if (string.IsNullOrEmpty(sourcePath))
                {
                    Debug.LogErrorFormat("[ShellProtector] The animator {0} isn't saved as an asset, so it can't be copied.", animator.name);
                    return null;
                }

                if (!targetPaths.TryGetValue(sourcePath, out string targetPath))
                {
                    // CopyAsset copies the file, so unsaved changes have to be written first.
                    AssetDatabase.SaveAssetIfDirty(animator);

                    if (!writer.CopyAssetToFolder(sourcePath, paths.Folders.AnimGuid, paths.ControllerName(animator), out targetPath))
                    {
                        Debug.LogErrorFormat("[ShellProtector] Failed to copy the animator {0}.", animator.name);
                        return null;
                    }
                    targetPaths.Add(sourcePath, targetPath);
                }

                AnimatorController copy = LoadCopy(animator, targetPath);
                if (copy == null)
                {
                    Debug.LogErrorFormat("[ShellProtector] Failed to load the copy of the animator {0}.", animator.name);
                    return null;
                }

                if (copiedBlendTrees.Add(copy))
                    CopyExternalBlendTrees(copy);
                duplicatedAnimators[i] = copy;
            }

            return duplicatedAnimators;
        }

        // CopyAsset keeps the file's local IDs, so a controller stored inside another asset is found in the copy by its ID.
        static AnimatorController LoadCopy(AnimatorController source, string targetPath)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string _, out long sourceId))
                return null;

            return AssetDatabase.LoadAllAssetsAtPath(targetPath).OfType<AnimatorController>().FirstOrDefault(c =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string _, out long id) && id == sourceId);
        }

        // Blend trees saved in their own asset (locomotion trees, CGE's generated trees) aren't copied with the controller
        // file, so the copy would still share them with the original and the build would change the original's animations.
        // The copy gets its own, stored in the copied controller.
        static void CopyExternalBlendTrees(AnimatorController controller)
        {
            string path = AssetDatabase.GetAssetPath(controller);
            var copies = new Dictionary<BlendTree, BlendTree>();
            foreach (AnimatorControllerLayer layer in controller.layers)
                CopyExternalBlendTrees(layer.stateMachine, path, copies);
        }

        static void CopyExternalBlendTrees(AnimatorStateMachine stateMachine, string path, Dictionary<BlendTree, BlendTree> copies)
        {
            if (stateMachine == null)
                return;

            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.motion is BlendTree tree)
                {
                    BlendTree copy = CopyExternalBlendTree(tree, path, copies);
                    if (copy != tree)
                        child.state.motion = copy;
                }
            }

            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
                CopyExternalBlendTrees(child.stateMachine, path, copies);
        }

        // A tree inside the controller stays as it is, but its children may still be external trees.
        static BlendTree CopyExternalBlendTree(BlendTree tree, string path, Dictionary<BlendTree, BlendTree> copies)
        {
            if (copies.TryGetValue(tree, out BlendTree copy))
                return copy;

            copy = tree;
            if (AssetDatabase.GetAssetPath(tree) != path)
            {
                // Instantiate trips an engine assertion on blend trees, so the copy is made field by field.
                copy = new BlendTree();
                EditorUtility.CopySerialized(tree, copy);
                copy.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(copy, path);
            }
            copies.Add(tree, copy);

            ChildMotion[] children = copy.children;
            bool changed = false;
            for (int i = 0; i < children.Length; ++i)
            {
                if (!(children[i].motion is BlendTree child))
                    continue;

                BlendTree childCopy = CopyExternalBlendTree(child, path, copies);
                if (childCopy == child)
                    continue;

                children[i].motion = childCopy;
                changed = true;
            }
            if (changed)
                copy.children = children;

            return copy;
        }

        public static void CreateKeyAnimations(string animationDir, OutputPaths paths, AssetWriter writer, GameObject[] objs)
        {
            string[] files = Directory.GetFiles(animationDir);
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (!filename.Contains(".anim"))
                    continue;
                if (filename.Contains("dummy"))
                    continue;

                if (!writer.CopyAssetToFolder(file, paths.Folders.AnimGuid, filename, out string path))
                    continue;
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                    continue;

                Match match = Regex.Match(filename, "key(\\d+).*?\\.anim");
                int n = 0;
                if (match.Success)
                    n = int.Parse(match.Groups[1].ToString());

                foreach (var obj in objs)
                {
                    AddKeyCurve(clip, obj, n, filename.Contains("_"));
                }
            }
        }

        static void AddKeyCurve(AnimationClip clip, GameObject obj, int keyIndex, bool secondKeyClip)
        {
            var binding = new EditorCurveBinding
            {
                path = GetAnimationPath(obj.transform),
                propertyName = "material." + ShaderProperties.KeyPrefix + keyIndex,
                type = obj.GetComponent<SkinnedMeshRenderer>() == null ? typeof(MeshRenderer) : typeof(SkinnedMeshRenderer)
            };

            AnimationUtility.SetEditorCurve(clip, binding, CreateKeyCurve(secondKeyClip));
        }

        static string GetAnimationPath(Transform transform)
        {
            var names = new List<string>();
            Transform current = transform;
            while (current != null && current.parent != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        // The key blend trees map a key parameter p in [-1, 1] to _Key = (p + 1) * 127, so the clips end at 0 and 254.
        // The OSC app sends key byte b as p = (b - 127) / 127. That lands exactly on VRChat's network quantization
        // (synced floats are multiples of 1/127), so remote players decode the same byte as the local player.
        // 255 can't be represented, which is why UserKey never produces it.
        public const float KeyCurveEnd = 2.1333334f;

        public static AnimationCurve CreateKeyCurve(bool secondKeyClip)
        {
            if (!secondKeyClip)
            {
                return new AnimationCurve(
                    new Keyframe(0, -128, -1919.995f, 59.999996f),
                    new Keyframe(KeyCurveEnd, 0, 59.999996f, -1919.995f)
                );
            }

            return new AnimationCurve(
                new Keyframe(0, UserKey.MaxKeyByte - 128, 423.3333f, 60.000004f),
                new Keyframe(KeyCurveEnd, UserKey.MaxKeyByte, 59.999996f, 60)
            );
        }

        private static BlendTree[] CreateKeyTree(string animationDir, int keyLength, float speed, UserKey key)
        {
            BlendTree[] tree = new BlendTree[keyLength];
            int offset = 16 - keyLength;
            for (int i = 0; i < keyLength; ++i)
            {
                BlendTree keyTree = new BlendTree();
                keyTree.name = "key" + i;
                keyTree.blendType = BlendTreeType.Simple1D;
                keyTree.blendParameter = ParameterManager.GetKeyName(i, key);
                keyTree.useAutomaticThresholds = false;

                Motion motion0 = AssetDatabase.LoadAssetAtPath(Path.Combine(animationDir, "key" + (i + offset) + ".anim"), typeof(AnimationClip)) as AnimationClip;
                Motion motion1 = AssetDatabase.LoadAssetAtPath(Path.Combine(animationDir, "key" + (i + offset) + "_2.anim"), typeof(AnimationClip)) as AnimationClip;

                keyTree.AddChild(motion0, -1);
                keyTree.AddChild(motion1, 1);

                ChildMotion[] motions = keyTree.children;
                for (int j = 0; j < motions.Length; ++j)
                    motions[j].timeScale = speed;
                keyTree.children = motions;

                tree[i] = keyTree;
            }
            return tree;
        }

        private static AnimatorConditionMode[] GetSwitchConditions(int switchCount, int index)
        {
            AnimatorConditionMode[] mode = new AnimatorConditionMode[switchCount];
            for (int i = 0; i < switchCount; ++i)
                mode[i] = AnimatorConditionMode.IfNot;
            for (int i = 0; i < switchCount; ++i)
            {
                if ((index & (1 << i)) != 0)
                    mode[i] = AnimatorConditionMode.If;
            }
            return mode;
        }

        private static void AddTransition(AnimatorStateTransition transition, int keyLength, int syncSize, int idx, UserKey key)
        {
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, ParameterManager.GetSyncLockName(syncSize, key));
            AnimatorConditionMode[] switchConditions = GetSwitchConditions(ShellProtector.GetRequiredSwitchCount(keyLength, syncSize), idx);
            for (int i = 0; i < switchConditions.Length; ++i)
                transition.AddCondition(switchConditions[i], 0, ParameterManager.GetSyncSwitchName(i, syncSize, key));
        }

        private static void AddParameters(AnimatorController anim, int keyLength, int syncSize, UserKey key)
        {
            AddParameterIfMissing(anim, new AnimatorControllerParameter
            {
                defaultFloat = 1.0f,
                name = "key_weight",
                type = AnimatorControllerParameterType.Float
            });

            AddParameterIfMissing(anim, new AnimatorControllerParameter
            {
                defaultBool = false,
                name = ParameterManager.GetIsLocalName(),
                type = AnimatorControllerParameterType.Bool
            });

            for (var i = 0; i < keyLength; ++i)
                AddParameterIfMissing(anim, ParameterManager.GetKeyName(i, key), AnimatorControllerParameterType.Float);

            AddParameterIfMissing(anim, ParameterManager.GetSyncLockName(syncSize, key), AnimatorControllerParameterType.Bool);
            var switchCount = ShellProtector.GetRequiredSwitchCount(keyLength, syncSize);

            if (!ParameterManager.IsOscMultiplexed(syncSize))
            {
                for (var i = 0; i < keyLength; ++i)
                    AddParameterIfMissing(anim, ParameterManager.GetSavedKeyName(i, key), AnimatorControllerParameterType.Float);
            }
            for (var i = 0; i < syncSize; ++i)
                AddParameterIfMissing(anim, ParameterManager.GetSyncedKeyName(i, syncSize, key), AnimatorControllerParameterType.Float);
            for (var i = 0; i < switchCount; ++i)
                AddParameterIfMissing(anim, ParameterManager.GetSyncSwitchName(i, syncSize, key), AnimatorControllerParameterType.Bool);
        }

        private static void AddParameterIfMissing(AnimatorController anim, string name, AnimatorControllerParameterType type)
        {
            if (anim.parameters.Any(p => p.name == name))
                return;

            anim.AddParameter(name, type);
        }

        private static void AddParameterIfMissing(AnimatorController anim, AnimatorControllerParameter parameter)
        {
            if (anim.parameters.Any(p => p.name == parameter.name))
                return;

            anim.AddParameter(parameter);
        }

        static readonly string[] keyLayerNames = { "ShellProtector", "ShellProtectorMux", "ShellProtectorDemux" };

        // Removes key layers left in the controller by an earlier build, for example when they were copied into the
        // original FX. They would drive the parameters of another salt or key size, so they are always built again.
        private static void RemoveKeyLayers(AnimatorController anim)
        {
            for (int i = anim.layers.Length - 1; i >= 0; --i)
            {
                if (keyLayerNames.Contains(anim.layers[i].name))
                    anim.RemoveLayer(i);
            }
        }

        public static void AddKeyLayer(AnimatorController anim, string animationDir, int keyLength, int syncSize, float speed, UserKey key)
        {
            RemoveKeyLayers(anim);

            AddParameters(anim, keyLength, syncSize, key);

            AddMuxLayer(anim, keyLength, syncSize, 0.15f, 0.15f, 0.1f, 0.1f, key);
            AddDemuxLayer(anim, keyLength, syncSize, key);

            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = anim.MakeUniqueLayerName("ShellProtector"),
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, anim);
            anim.AddLayer(new AnimatorControllerLayer { name = stateMachine.name, defaultWeight = 1.0f, stateMachine = stateMachine });

            var layer = anim.layers[anim.layers.Length - 1];
            var state = layer.stateMachine.AddState("keys");

            BlendTree rootTree = new BlendTree
            {
                name = "key_root",
                blendType = BlendTreeType.Direct,
                blendParameter = "key_weight"
            };
            AssetDatabase.AddObjectToAsset(rootTree, anim);
            state.motion = rootTree;

            var keyTrees = CreateKeyTree(animationDir, keyLength, speed, key);
            for (int i = 0; i < keyLength; ++i)
            {
                rootTree.AddChild(keyTrees[i]);
                AssetDatabase.AddObjectToAsset(keyTrees[i], anim);
            }

            ChildMotion[] children = rootTree.children;
            for (int i = 0; i < children.Length; ++i)
                children[i].directBlendParameter = "key_weight";

            rootTree.children = children;
        }

        private static void AddSyncEnabledCondition(AnimatorStateTransition transition)
        {
            transition.AddCondition(AnimatorConditionMode.If, 0, ParameterManager.GetIsLocalName());
        }

        // Each step: lock on, wait `interval` (`delay` for the first step), change the synced keys, wait `settleTime`,
        // lock off, wait `unlockedTime`, next step. VRChat sends synced parameters at most 10 times a second, so the
        // waits have to be long enough for remote players to see every lock state.
        private static void AddMuxLayer(AnimatorController anim, int keyLength, int syncSize, float settleTime, float unlockedTime, float interval, float delay, UserKey key)
        {
            // With a sync speed of 1 the OSC app multiplexes the key itself.
            if (ParameterManager.IsOscMultiplexed(syncSize))
                return;

            var stateMachine = new AnimatorStateMachine
            {
                name = anim.MakeUniqueLayerName("ShellProtectorMux"),
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, anim);
            anim.AddLayer(new AnimatorControllerLayer { name = stateMachine.name, defaultWeight = 1.0f, stateMachine = stateMachine });
            var layer = anim.layers[anim.layers.Length - 1];
            var idle = layer.stateMachine.AddState("Idle", new Vector3(0, 0));

            var steps = keyLength / syncSize;
            var syncStates = new AnimatorState[steps];
            var lockStates = new AnimatorState[steps];
            var unlockStates = new AnimatorState[steps];
            const int x = 250;
            const int y = 80;
            layer.stateMachine.entryPosition = new Vector3(-x, 0);
            layer.stateMachine.exitPosition = new Vector3(x * 4, y * (steps - 1));
            for (var step = 0; step < steps; step++)
            {
                var lockState = layer.stateMachine.AddState("mux" + step + "_lock", new Vector3(x * 1, y * step));
                var syncState = layer.stateMachine.AddState("mux" + step + "_sync", new Vector3(x * 2, y * step));
                var unlockState = layer.stateMachine.AddState("mux" + step + "_unlock", new Vector3(x * 3, y * step));

                // A parameter driver runs when its state is entered, which is the start of the transition into it, and
                // the next transition can't start until that one finishes. So each wait is the duration of the
                // transition into the state whose driver acts before it:
                // - lock -> sync: the lock stays on after the synced keys change. Remote players interpolate synced
                //   floats, and the demux layer must not copy them before they settle.
                // - sync -> unlock: the lock stays off before the next step turns it on again.
                var lockToSync = lockState.AddTransition(syncState);
                lockToSync.hasExitTime = false;
                lockToSync.duration = settleTime;
                AddSyncEnabledCondition(lockToSync);

                var syncToUnlock = syncState.AddTransition(unlockState);
                syncToUnlock.hasExitTime = false;
                syncToUnlock.duration = unlockedTime;
                AddSyncEnabledCondition(syncToUnlock);

                if (step == 0) // first step
                {
                    var transition = idle.AddTransition(lockState);
                    transition.hasExitTime = false;
                    transition.duration = delay;
                    AddSyncEnabledCondition(transition);
                }
                else
                {
                    var previousUnlock = unlockStates[step - 1];
                    var transition = previousUnlock.AddTransition(lockState);
                    transition.hasExitTime = false;
                    transition.duration = interval;
                    AddSyncEnabledCondition(transition);
                }

                // Exit goes back to Idle, so the mux keeps cycling and picks up new saved keys. This also covers a
                // single step (sync speed equal to the key length).
                if (step == steps - 1)
                {
                    var exit = unlockState.AddExitTransition();
                    exit.hasExitTime = false;
                    exit.duration = 0;
                    AddSyncEnabledCondition(exit);
                }

                var lockDriver = lockState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                var syncDriver = syncState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                var unlockDriver = unlockState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

                lockDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = ParameterManager.GetSyncLockName(syncSize, key),
                    value = 1
                });

                // The demux layer copies the synced keys into the key parameters, here and on remote players.
                for (var i = 0; i < syncSize; i++)
                {
                    syncDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Copy,
                        name = ParameterManager.GetSyncedKeyName(i, syncSize, key),
                        source = ParameterManager.GetSavedKeyName(step * syncSize + i, key)
                    });
                }

                for (var i = 0; i < ShellProtector.GetRequiredSwitchCount(keyLength, syncSize); i++)
                {
                    syncDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = ParameterManager.GetSyncSwitchName(i, syncSize, key),
                        value = (step & (1 << i)) != 0 ? 1 : 0
                    });
                }

                unlockDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = ParameterManager.GetSyncLockName(syncSize, key),
                    value = 0
                });

                syncStates[step] = syncState;
                lockStates[step] = lockState;
                unlockStates[step] = unlockState;
            }
        }

        private static void AddDemuxLayer(AnimatorController anim, int keyLength, int syncSize, UserKey key)
        {
            var stateMachine = new AnimatorStateMachine
            {
                name = anim.MakeUniqueLayerName("ShellProtectorDemux"),
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, anim);
            anim.AddLayer(new AnimatorControllerLayer { name = stateMachine.name, defaultWeight = 1.0f, stateMachine = stateMachine });

            var layer = anim.layers[anim.layers.Length - 1];
            var state = layer.stateMachine.AddState("empty");

            var transition = layer.stateMachine.AddAnyStateTransition(state);
            transition.canTransitionToSelf = false;
            transition.exitTime = 0;
            transition.duration = 0;
            transition.hasExitTime = false;
            transition.AddCondition(AnimatorConditionMode.If, 0, ParameterManager.GetSyncLockName(syncSize, key));

            for (var i = 0; i < keyLength / syncSize; ++i)
            {
                var keyState = layer.stateMachine.AddState("key" + i);

                for (var j = 0; j < syncSize; j++)
                {
                    var behaviour = keyState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    behaviour.parameters.Add(new VRCAvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Copy,
                        source = ParameterManager.GetSyncedKeyName(j, syncSize, key),
                        name = ParameterManager.GetKeyName(i * syncSize + j, key)
                    });
                }

                transition = layer.stateMachine.AddAnyStateTransition(keyState);
                transition.canTransitionToSelf = false;
                transition.exitTime = 0;
                transition.duration = 0;
                transition.hasExitTime = false;
                AddTransition(transition, keyLength, syncSize, i, key);
            }
        }

        public static bool IsMaterialInClip(AnimationClip clip, Material originalMaterial)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (IsMaterialBinding(binding) && AnimationUtility.GetObjectReferenceCurve(clip, binding).Any(k => k.value == originalMaterial))
                    return true;
            }
            return false;
        }

        static bool IsMaterialBinding(EditorCurveBinding binding)
        {
            return (binding.type == typeof(MeshRenderer) || binding.type == typeof(SkinnedMeshRenderer)) && binding.propertyName.StartsWith("m_Materials");
        }

        // Points the material swaps of the controllers at resolve(material) in one walk over their states and blend trees,
        // so each clip is read once however many states use it and however many materials the build replaces. A clip
        // that changes is replaced by getWritable(clip), which is the clip itself when the build owns it.
        public static void ReplaceMaterials(IEnumerable<AnimatorController> controllers, System.Func<Material, Material> resolve,
            System.Func<AnimationClip, AnimationClip> getWritable)
        {
            var walk = new MaterialSwapWalk(resolve, getWritable);
            foreach (AnimatorController controller in controllers.Where(c => c != null).Distinct())
            {
                foreach (AnimatorControllerLayer layer in controller.layers)
                {
                    if (layer.name == "ShellProtectorDriver" || layer.name == "ShellProtector" || layer.stateMachine == null)
                        continue;
                    walk.StateMachine(layer.stateMachine);
                }
            }
        }

        sealed class MaterialSwapWalk
        {
            readonly System.Func<Material, Material> resolve;
            readonly System.Func<AnimationClip, AnimationClip> getWritable;
            // Each clip seen and the clip that replaces it, which is the clip itself when nothing in it changes.
            readonly Dictionary<AnimationClip, AnimationClip> clips = new Dictionary<AnimationClip, AnimationClip>();
            readonly HashSet<AnimatorStateMachine> stateMachines = new HashSet<AnimatorStateMachine>();
            readonly HashSet<BlendTree> blendTrees = new HashSet<BlendTree>();

            public MaterialSwapWalk(System.Func<Material, Material> resolve, System.Func<AnimationClip, AnimationClip> getWritable)
            {
                this.resolve = resolve;
                this.getWritable = getWritable;
            }

            public void StateMachine(AnimatorStateMachine stateMachine)
            {
                if (!stateMachines.Add(stateMachine))
                    return;
                foreach (ChildAnimatorState child in stateMachine.states)
                {
                    AnimatorState state = child.state;
                    Motion motion = Replace(state.motion);
                    if (motion != state.motion)
                        state.motion = motion;
                }
                foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
                    StateMachine(child.stateMachine);
            }

            Motion Replace(Motion motion)
            {
                if (motion is AnimationClip clip)
                    return Replace(clip);
                if (motion is BlendTree tree && blendTrees.Add(tree))
                {
                    ChildMotion[] children = tree.children;
                    bool changed = false;
                    for (int i = 0; i < children.Length; ++i)
                    {
                        Motion child = Replace(children[i].motion);
                        if (child == children[i].motion)
                            continue;
                        children[i].motion = child;
                        changed = true;
                    }
                    if (changed)
                        tree.children = children;
                }
                return motion;
            }

            AnimationClip Replace(AnimationClip clip)
            {
                if (clips.TryGetValue(clip, out AnimationClip result))
                    return result;

                var bindings = new List<EditorCurveBinding>();
                var curves = new List<ObjectReferenceKeyframe[]>();
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (!IsMaterialBinding(binding))
                        continue;
                    ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    bool changed = false;
                    for (int i = 0; i < keyframes.Length; ++i)
                    {
                        if (!(keyframes[i].value is Material material))
                            continue;
                        Material replacement = resolve(material);
                        if (replacement == material)
                            continue;
                        keyframes[i].value = replacement;
                        changed = true;
                    }
                    if (changed)
                    {
                        bindings.Add(binding);
                        curves.Add(keyframes);
                    }
                }

                result = clip;
                if (bindings.Count > 0)
                {
                    result = getWritable(clip);
                    AnimationUtility.SetObjectReferenceCurves(result, bindings.ToArray(), curves.ToArray());
                }
                clips[clip] = result;
                clips[result] = result;
                return result;
            }
        }
    }
}
#endif
