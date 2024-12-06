#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Shell.Protector
{
    public class AnimatorManager : ScriptableObject
    {
        Dictionary<AnimationClip, AnimationClip> encryptedClip = new Dictionary<AnimationClip, AnimationClip>();

        static string curve1 = @"
  - curve:
      serializedVersion: 2
      m_Curve:
      - serializedVersion: 3
        time: 0
        value: -128
        inSlope: -1919.995
        outSlope: 59.999996
        tangentMode: 69
        weightedMode: 0
        inWeight: 0.33333334
        outWeight: 0.33333334
      - serializedVersion: 3
        time: 2.1333334
        value: 0
        inSlope: 59.999996
        outSlope: -1919.995
        tangentMode: 69
        weightedMode: 0
        inWeight: 0.33333334
        outWeight: 0.33333334
      m_PreInfinity: 2
      m_PostInfinity: 2
      m_RotationOrder: 4
    attribute: material._Key0
    path: Body
    classID: 137
    script: {fileID: 0}";

        static string curve2 = @"
  - curve:
      serializedVersion: 2
      m_Curve:
      - serializedVersion: 3
        time: 0
        value: 128
        inSlope: 423.3333
        outSlope: 60.000004
        tangentMode: 69
        weightedMode: 0
        inWeight: 0.33333334
        outWeight: 0.33333334
      - serializedVersion: 3
        time: 2.1333334
        value: 256
        inSlope: 59.999996
        outSlope: 60
        tangentMode: 69
        weightedMode: 0
        inWeight: 0.33333334
        outWeight: 0.33333334
      m_PreInfinity: 2
      m_PostInfinity: 2
      m_RotationOrder: 4
    attribute: material._Key0
    path: Body
    classID: 137
    script: {fileID: 0}";
        static string curve3 = @"
  - serializedVersion: 2
    curve:
      serializedVersion: 2
      m_Curve:
      - serializedVersion: 3
        time: 0
        value: 0
        inSlope: 0
        outSlope: 0
        tangentMode: 136
        weightedMode: 0
        inWeight: 0.33333334
        outWeight: 0.33333334
      m_PreInfinity: 2
      m_PostInfinity: 2
      m_RotationOrder: 4
    attribute: material._fallback
    path: Body
    classID: 137
    script: {fileID: 0}
    flags: 16";
        public static AnimatorController DuplicateAnimator(RuntimeAnimatorController anim, string new_dir)
        {
            string dir = AssetDatabase.GetAssetPath(anim);
            string output = Path.Combine(new_dir, anim.name + "_encrypted.anim");
            AssetDatabase.CopyAsset(dir, output);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath(output, typeof(RuntimeAnimatorController)) as AnimatorController;
        }

        public static void CreateKeyAniamtions(string animation_dir, string new_dir, GameObject[] objs)
        {
            string[] files = Directory.GetFiles(animation_dir);
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (!filename.Contains(".anim"))
                    continue;
                if (filename.Contains("dummy"))
                    continue;
                if (filename.Contains("FallbackOff"))
                    continue;
                
                string path = Path.Combine(new_dir, filename);
                AssetDatabase.CopyAsset(file, path);
                string anim = File.ReadAllText(path);

                Match match = Regex.Match(filename, "key(\\d+).*?\\.anim");
                int n = 0;
                if (match.Success)
                    n = int.Parse(match.Groups[1].ToString());

                foreach (var obj in objs)
                {
                    string hr_path = obj.transform.GetHierarchyPath();
                    hr_path = Regex.Replace(hr_path, ".*?/(.*)", "'$1'");

                    string curve;

                    if (!filename.Contains("_"))
                        curve = Regex.Replace(curve1, "attribute: material._Key\\d+", "attribute: material._Key" + n);
                    else
                        curve = Regex.Replace(curve2, "attribute: material._Key\\d+", "attribute: material._Key" + n);
                    curve = Regex.Replace(curve, "path: Body", "path: " + hr_path);
                    //SkinnedMeshRender classID:137
                    //MeshRenderer classID:23
                    if (obj.GetComponent<SkinnedMeshRenderer>() == null)
                        curve = Regex.Replace(curve, "classID: 137", "classID: 23");

                    anim = Regex.Replace(anim, "m_FloatCurves:", "m_FloatCurves:" + curve);
                    anim = Regex.Replace(anim, "m_EditorCurves:", "m_EditorCurves:" + curve);
                }
                File.WriteAllText(path, anim);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static AnimationClip CreateFallbackAniamtions(string animation_dir, string new_dir, GameObject[] objs)
        {
            string newPath = Path.Combine(new_dir, "FallbackOff.anim");

            AssetDatabase.CopyAsset(animation_dir, newPath);

            string anim = File.ReadAllText(newPath);

            foreach (var obj in objs)
            {
                if (obj.name == "Body")
                    continue;

                string hr_path = obj.transform.GetHierarchyPath();
                hr_path = Regex.Replace(hr_path, ".*?/(.*)", "'$1'");

                string curve = Regex.Replace(curve3, "path: Body", "path: " + hr_path);
                //SkinnedMeshRender classID:137
                //MeshRenderer classID:23
                if (obj.GetComponent<SkinnedMeshRenderer>() == null)
                    curve = Regex.Replace(curve, "classID: 137", "classID: 23");

                anim = Regex.Replace(anim, "m_FloatCurves:", "m_FloatCurves:" + curve);
                anim = Regex.Replace(anim, "m_EditorCurves:", "m_EditorCurves:" + curve);
            }
            File.WriteAllText(newPath, anim);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(newPath);
        }

        private static BlendTree[] CreateKeyTree(string animation_dir, int key_length, float speed)
        {
            BlendTree[] tree = new BlendTree[key_length];
            int offset = 16 - key_length;
            for (int i = 0; i < key_length; ++i)
            {
                BlendTree tree_key = new BlendTree();
                tree_key.name = "key" + i;
                tree_key.blendType = BlendTreeType.Direct;
                tree_key.blendParameter = "key_weight";
                tree_key.blendType = BlendTreeType.Simple1D;
                tree_key.blendParameter = "pkey" + i;
                tree_key.useAutomaticThresholds = false;

                Motion motion0 = AssetDatabase.LoadAssetAtPath(Path.Combine(animation_dir, "key" + (i + offset) + ".anim"), typeof(AnimationClip)) as AnimationClip;
                Motion motion1 = AssetDatabase.LoadAssetAtPath(Path.Combine(animation_dir, "key" + (i + offset) + "_2.anim"), typeof(AnimationClip)) as AnimationClip;

                tree_key.AddChild(motion0, -1);
                tree_key.AddChild(motion1, 1);

                ChildMotion[] motions = tree_key.children;
                for (int j = 0; j < motions.Length; ++j)
                    motions[j].timeScale = speed;
                tree_key.children = motions;

                tree[i] = tree_key;
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
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, "encrypt_lock");
            AnimatorConditionMode[] switchConditions = GetSwitchConditions(ShellProtector.GetRequiredSwitchCount(keyLength, syncSize), idx);
            for (int i = 0; i < switchConditions.Length; ++i)
                transition.AddCondition(switchConditions[i], 0, "encrypt_switch" + i);
        }
        public static void AddParameter(AnimatorController anim, int key_length, int sync_size, bool optimize)
        {
            var paramters = anim.parameters;
            for (int i = 0; i < paramters.Length; ++i)
            {
                string name = paramters[i].name;
                if (name == "key_weight")
                    return;
            }
            anim.AddParameter(new AnimatorControllerParameter() { defaultFloat = 1.0f, name = "key_weight", type = AnimatorControllerParameterType.Float });

            for (int i = 0; i < key_length; ++i)
                anim.AddParameter("pkey" + i, AnimatorControllerParameterType.Float);

            if (optimize)
            {
                for(int i = 0; i < sync_size; i++)
                    anim.AddParameter(ParameterManager.GetPKeySyncParameterName(i), AnimatorControllerParameterType.Float);
                anim.AddParameter("encrypt_lock", AnimatorControllerParameterType.Bool);
                int switch_count = ShellProtector.GetRequiredSwitchCount(key_length, sync_size);
                for (int i = 0; i < switch_count; ++i)
                    anim.AddParameter("encrypt_switch" + i, AnimatorControllerParameterType.Bool);
            }
        }
        public static void AddKeyLayer(AnimatorController anim, string animation_dir, int key_length, int sync_size, float speed = 10.0f, bool optimize = false)
        {
            AddParameter(anim, key_length, sync_size,  optimize);

            if (optimize)
            {
                AddKeyLayerMultiplexing(anim, animation_dir, key_length, sync_size, speed);
                return;
            }

            var layers = anim.layers;
            foreach (var _layer in layers)
            {
                if (_layer.name == "ShellProtector")
                    return;
            }

            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = anim.MakeUniqueLayerName("ShellProtector"),
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, anim);
            anim.AddLayer(new AnimatorControllerLayer { name = stateMachine.name, defaultWeight = 1.0f, stateMachine = stateMachine });

            var layer = anim.layers[anim.layers.Length - 1];
            var state = layer.stateMachine.AddState("keys");

            BlendTree tree_root = new BlendTree
            {
                name = "key_root",
                blendType = BlendTreeType.Direct,
                blendParameter = "key_weight"
            };
            AssetDatabase.AddObjectToAsset(tree_root, anim);
            state.motion = tree_root;

            var key_tree = CreateKeyTree(animation_dir, key_length, speed);
            for (int i = 0; i < key_length; ++i)
            {
                tree_root.AddChild(key_tree[i]);
                AssetDatabase.AddObjectToAsset(key_tree[i], anim);
            }

            ChildMotion[] children = tree_root.children;
            for (int i = 0; i < children.Length; ++i)
                children[i].directBlendParameter = "key_weight";

            tree_root.children = children;
        }

        public static void AddKeyLayerMultiplexing(AnimatorController anim, string animation_dir, int key_length, int sync_size, float speed = 10.0f)
        {
            var layers = anim.layers;
            foreach (var _layer in layers)
            {
                if (_layer.name == "ShellProtectorDriver")
                    return;
            }

            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = anim.MakeUniqueLayerName("ShellProtectorDriver"),
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
            transition.AddCondition(AnimatorConditionMode.If, 0, "encrypt_lock");

            for (int i = 0; i < key_length / sync_size; ++i)
            {
                var key_state = layer.stateMachine.AddState("key" + i);

                for (var j = 0; j < sync_size; j++)
                {
                    var behaviour = key_state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    var behaviour_param = new VRCAvatarParameterDriver.Parameter
                    {
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy,
                        name = "pkey" + (i * sync_size + j),
                        source = ParameterManager.GetPKeySyncParameterName(j)
                    };
                    behaviour.parameters.Add(behaviour_param);
                }

                transition = layer.stateMachine.AddAnyStateTransition(key_state);
                transition.canTransitionToSelf = false;
                transition.exitTime = 0;
                transition.duration = 0;
                transition.hasExitTime = false;
                AddTransition(transition, key_length, sync_size, i);
            }

            AddKeyLayer(anim, animation_dir, key_length, sync_size, speed, false);
        }

        public static void AddFallbackLayer(AnimatorController anim, AnimationClip fallbackAnimation, float time = 3)
        {
            var layers = anim.layers;
            foreach (var _layer in layers)
            {
                if (_layer.name == "ShellProtectorFallback")
                    return;
            }

            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = anim.MakeUniqueLayerName("ShellProtectorFallback"),
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, anim);
            anim.AddLayer(new AnimatorControllerLayer { name = stateMachine.name, defaultWeight = 1.0f, stateMachine = stateMachine });

            var layer = anim.layers[anim.layers.Length - 1];
            var state = layer.stateMachine.AddState("empty");
            state.writeDefaultValues = true;
            var fallbackState = layer.stateMachine.AddState("fallbackState");

            var transition = state.AddTransition(fallbackState);
            transition.canTransitionToSelf = false;
            transition.exitTime = time;
            transition.duration = 0;
            transition.hasExitTime = true;

            fallbackState.motion = fallbackAnimation;
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

        void SearchStateMachine(AnimatorStateMachine stateMachine, Material targetMaterial, Material encrypted, bool clone, string clonePath)
        {
            for (int i = 0; i < stateMachine.states.Length; i++)
            {
                ChildAnimatorState state = stateMachine.states[i];
                AnimationClip clip = SearchMotion(state.state.motion, targetMaterial, encrypted, clone, clonePath);
                if (clip != null)
                {
                    state.state.motion = clip;
                    stateMachine.states[i] = state;
                }
            }

            foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
            {
                SearchStateMachine(childStateMachine.stateMachine, targetMaterial, encrypted, clone, clonePath);
            }
        }

        AnimationClip SearchMotion(Motion motion, Material targetMaterial, Material encrypted, bool clone, string clonePath)
        {
            if (motion is AnimationClip clip)
            {
                if (IsMaterialInClip(clip, targetMaterial))
                {
                    Debug.LogFormat("{0} in {1}", targetMaterial.name, clip.name);
                    if (clone)
                    {
                        string path = AssetDatabase.GetAssetPath(clip);
                        string copyPath = Path.Combine(clonePath, clip.name + "_encrypted.anim");

                        if (encryptedClip.ContainsKey(clip))
                        {
                            clip = encryptedClip[clip];
                        }
                        else
                        {
                            if (!AssetDatabase.CopyAsset(path, copyPath))
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
                    AnimationClip result = SearchMotion(childMotion.motion, targetMaterial, encrypted, clone, clonePath);
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

        public void ChangeAnimationMaterial(AnimatorController anim, Material original, Material encrypted, bool clone, string clonePath)
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
                SearchStateMachine(stateMachine, original, encrypted, clone, clonePath);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif