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
        Dictionary<AnimationClip, AnimationClip> encryptedClip = new Dictionary<AnimationClip, AnimationClip>();

        public static AnimatorController DuplicateAnimator(RuntimeAnimatorController anim, OutputPaths paths, AssetWriter writer)
        {
            return DuplicateAnimators(new[] { anim }, paths, writer)[0];
        }

        public static AnimatorController[] DuplicateAnimators(RuntimeAnimatorController[] animators, OutputPaths paths, AssetWriter writer)
        {
            var sourcePaths = new string[animators.Length];
            var targetPaths = new Dictionary<string, string>();

            for (int i = 0; i < animators.Length; ++i)
            {
                RuntimeAnimatorController animator = animators[i];
                if (animator == null)
                    continue;

                string sourcePath = AssetDatabase.GetAssetPath(animator);
                sourcePaths[i] = sourcePath;
                if (targetPaths.ContainsKey(sourcePath))
                    continue;

                if (!writer.CopyAssetToFolder(sourcePath, paths.Folders.AnimGuid, paths.ControllerName(animator), out string targetPath))
                {
                    Debug.LogErrorFormat("Failed to copy a animator: {0}", animator.name);
                    continue;
                }

                targetPaths.Add(sourcePath, targetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var duplicatedAnimators = new AnimatorController[animators.Length];
            for (int i = 0; i < sourcePaths.Length; ++i)
            {
                if (!string.IsNullOrEmpty(sourcePaths[i]) && targetPaths.TryGetValue(sourcePaths[i], out string targetPath))
                    duplicatedAnimators[i] = AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
            }

            return duplicatedAnimators;
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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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

        static AnimationCurve CreateKeyCurve(bool secondKeyClip)
        {
            if (!secondKeyClip)
            {
                return new AnimationCurve(
                    new Keyframe(0, -128, -1919.995f, 59.999996f),
                    new Keyframe(2.1333334f, 0, 59.999996f, -1919.995f)
                );
            }

            return new AnimationCurve(
                new Keyframe(0, 128, 423.3333f, 60.000004f),
                new Keyframe(2.1333334f, 256, 59.999996f, 60)
            );
        }

        private static BlendTree[] CreateKeyTree(string animationDir, int keyLength, float speed)
        {
            BlendTree[] tree = new BlendTree[keyLength];
            int offset = 16 - keyLength;
            for (int i = 0; i < keyLength; ++i)
            {
                BlendTree keyTree = new BlendTree();
                keyTree.name = "key" + i;
                keyTree.blendType = BlendTreeType.Simple1D;
                keyTree.blendParameter = ParameterManager.GetKeyName(i);
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

        private static void AddTransition(AnimatorStateTransition transition, int keyLength, int syncSize, int idx)
        {
            bool bLegacy = syncSize == 1;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, ParameterManager.GetSyncLockName(bLegacy));
            AnimatorConditionMode[] switchConditions = GetSwitchConditions(ShellProtector.GetRequiredSwitchCount(keyLength, syncSize), idx);
            for (int i = 0; i < switchConditions.Length; ++i)
                transition.AddCondition(switchConditions[i], 0, ParameterManager.GetSyncSwitchName(i, bLegacy));
        }

        private static void AddParameters(AnimatorController anim, int keyLength, int syncSize)
        {
            bool bLegacy = syncSize == 1;
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
                AddParameterIfMissing(anim, ParameterManager.GetKeyName(i), AnimatorControllerParameterType.Float);

            AddParameterIfMissing(anim, ParameterManager.GetSyncLockName(bLegacy), AnimatorControllerParameterType.Bool);
            var switchCount = ShellProtector.GetRequiredSwitchCount(keyLength, syncSize);

            if (!bLegacy)
            {
                for (var i = 0; i < keyLength; ++i)
                    AddParameterIfMissing(anim, ParameterManager.GetSavedKeyName(i), AnimatorControllerParameterType.Float);
            }
            for (var i = 0; i < syncSize; ++i)
                AddParameterIfMissing(anim, ParameterManager.GetSyncedKeyName(i, bLegacy), AnimatorControllerParameterType.Float);
            for (var i = 0; i < switchCount; ++i)
                AddParameterIfMissing(anim, ParameterManager.GetSyncSwitchName(i, bLegacy), AnimatorControllerParameterType.Bool);
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

        public static void AddKeyLayer(AnimatorController anim, string animationDir, int keyLength, int syncSize, float speed)
        {
            if (anim.layers.Any(l => l.name == "ShellProtector")) return;

            AddParameters(anim, keyLength, syncSize);

            AddMuxLayer(anim, keyLength, syncSize, 0.15f, 0.1f, 1f); // 10hz
            AddDemuxLayer(anim, keyLength, syncSize);

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

            var keyTrees = CreateKeyTree(animationDir, keyLength, speed);
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

        private static void AddMuxLayer(AnimatorController anim, int keyLength, int syncSize, float unlockDelay, float interval, float delay)
        {
            if (anim.layers.Any(l => l.name == "ShellProtectorMux")) 
                return;

            bool bLegacy = syncSize == 1;

            if (bLegacy)
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

                var lockToSync = lockState.AddTransition(syncState);
                lockToSync.hasExitTime = false;
                lockToSync.duration = 0;
                AddSyncEnabledCondition(lockToSync);

                var syncToUnlock = syncState.AddTransition(unlockState);
                syncToUnlock.hasExitTime = false;
                syncToUnlock.duration = unlockDelay;
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
                    if (step == steps - 1)
                    {
                        var exit = unlockState.AddExitTransition(); // last step exit
                        exit.hasExitTime = false;
                        exit.duration = 0;
                        AddSyncEnabledCondition(exit);
                    }
                    var previousUnlock = unlockStates[step - 1];
                    var transition = previousUnlock.AddTransition(lockState);
                    transition.hasExitTime = false;
                    transition.duration = interval;
                    AddSyncEnabledCondition(transition);
                }

                var lockDriver = lockState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                var syncDriver = syncState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                var unlockDriver = unlockState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

                lockDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = ParameterManager.GetSyncLockName(bLegacy),
                    value = 1
                });

                for (var i = 0; i < syncSize; i++)
                {
                    syncDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Copy,
                        name = ParameterManager.GetKeyName(step * syncSize + i),
                        source = ParameterManager.GetSavedKeyName(step * syncSize + i)
                    });
                }

                for (var i = 0; i < ShellProtector.GetRequiredSwitchCount(keyLength, syncSize); i++)
                {
                    syncDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = ParameterManager.GetSyncSwitchName(i, bLegacy),
                        value = (step & (1 << i)) != 0 ? 1 : 0
                    });
                }

                unlockDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = ParameterManager.GetSyncLockName(bLegacy),
                    value = 0
                });

                syncStates[step] = syncState;
                lockStates[step] = lockState;
                unlockStates[step] = unlockState;
            }
        }

        private static void AddDemuxLayer(AnimatorController anim, int keyLength, int syncSize)
        {
            bool bLegacy = syncSize == 1;
            if (anim.layers.Any(l => l.name == "ShellProtectorDemux")) return;

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
            transition.AddCondition(AnimatorConditionMode.If, 0, ParameterManager.GetSyncLockName(bLegacy));

            for (var i = 0; i < keyLength / syncSize; ++i)
            {
                var keyState = layer.stateMachine.AddState("key" + i);

                for (var j = 0; j < syncSize; j++)
                {
                    var behaviour = keyState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    behaviour.parameters.Add(new VRCAvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Copy,
                        source = ParameterManager.GetSyncedKeyName(j, bLegacy),
                        name = ParameterManager.GetKeyName(i * syncSize + j)
                    });
                }

                transition = layer.stateMachine.AddAnyStateTransition(keyState);
                transition.canTransitionToSelf = false;
                transition.exitTime = 0;
                transition.duration = 0;
                transition.hasExitTime = false;
                AddTransition(transition, keyLength, syncSize, i);
            }
        }

        public static bool IsMaterialInClip(AnimationClip clip, Material originalMaterial)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (EditorCurveBinding binding in bindings)
            {
                if ((binding.type == typeof(MeshRenderer) || binding.type == typeof(SkinnedMeshRenderer)) && binding.propertyName.StartsWith("m_Materials"))
                {
                    ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);

                    for (int i = 0; i < keyframes.Length; i++)
                    {
                        if (keyframes[i].value == originalMaterial)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        public static void ChangeMaterialInClip(AnimationClip clip, Material source, Material target)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (EditorCurveBinding binding in bindings)
            {
                if ((binding.type == typeof(MeshRenderer) || binding.type == typeof(SkinnedMeshRenderer)) && binding.propertyName.StartsWith("m_Materials"))
                {
                    ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    ObjectReferenceKeyframe[] newKeyframes = new ObjectReferenceKeyframe[keyframes.Length];

                    bool curveChanged = false;
                    for (int i = 0; i < keyframes.Length; i++)
                    {
                        newKeyframes[i] = new ObjectReferenceKeyframe
                        {
                            time = keyframes[i].time,
                            value = keyframes[i].value == source ? target : keyframes[i].value
                        };

                        if (keyframes[i].value == source)
                        {
                            curveChanged = true;
                        }
                    }
                    if (curveChanged)
                    {
                        AnimationUtility.SetObjectReferenceCurve(clip, binding, newKeyframes);
                    }
                }
            }
        }

        void SearchStateMachine(AnimatorStateMachine stateMachine, Material targetMaterial, Material encrypted, bool clone, OutputPaths paths, AssetWriter writer)
        {
            for (int i = 0; i < stateMachine.states.Length; i++)
            {
                ChildAnimatorState state = stateMachine.states[i];
                AnimationClip clip = SearchMotion(state.state.motion, targetMaterial, encrypted, clone, paths, writer);
                if (clip != null)
                {
                    state.state.motion = clip;
                    stateMachine.states[i] = state;
                }
            }

            foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
            {
                SearchStateMachine(childStateMachine.stateMachine, targetMaterial, encrypted, clone, paths, writer);
            }
        }

        AnimationClip SearchMotion(Motion motion, Material targetMaterial, Material encrypted, bool clone, OutputPaths paths, AssetWriter writer)
        {
            if (motion is AnimationClip clip)
            {
                if (IsMaterialInClip(clip, targetMaterial))
                {
                    Debug.LogFormat("{0} in {1}", targetMaterial.name, clip.name);
                    if (clone)
                    {
                        string path = AssetDatabase.GetAssetPath(clip);

                        if (encryptedClip.ContainsKey(clip))
                        {
                            clip = encryptedClip[clip];
                        }
                        else
                        {
                            if (!writer.CopyAssetToFolder(path, paths.Folders.AnimGuid, paths.AnimationClipName(clip, "_encrypted"), out string copyPath))
                            {
                                Debug.LogError("Copy error: " + copyPath);
                                return null;
                            }
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                            var newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(copyPath);
                            encryptedClip.Add(clip, newClip);
                            clip = newClip;
                        }
                    }
                    ChangeMaterialInClip(clip, targetMaterial, encrypted);
                    return clip;
                }
            }
            else if (motion is BlendTree blendTree)
            {
                for (int i = 0; i < blendTree.children.Length; ++i)
                {
                    ChildMotion childMotion = blendTree.children[i];
                    AnimationClip result = SearchMotion(childMotion.motion, targetMaterial, encrypted, clone, paths, writer);
                    if (result != null)
                    {
                        childMotion.motion = result;
                        ChildMotion[] newChildren = new ChildMotion[blendTree.children.Length];
                        blendTree.children.CopyTo(newChildren, 0);
                        newChildren[i] = childMotion;
                        blendTree.children = newChildren;
                    }
                }
            }
            return null;
        }

        public void ChangeAnimationMaterial(AnimatorController anim, Material original, Material encrypted, bool clone, OutputPaths paths, AssetWriter writer)
        {
            if (anim == null || original == null)
                return;

            var layers = anim.layers;
            foreach (var layer in layers)
            {
                if (layer.name == "ShellProtectorDriver")
                    continue;
                if (layer.name == "ShellProtector")
                    continue;
                var stateMachine = layer.stateMachine;
                if (stateMachine == null)
                    continue;
                SearchStateMachine(stateMachine, original, encrypted, clone, paths, writer);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
