#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public static class AnimatorManager
{
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
    public static AnimatorController DuplicateAnimator(RuntimeAnimatorController anim, string new_dir)
    {
        string dir = AssetDatabase.GetAssetPath(anim);
        string output = Path.Combine(new_dir, anim.name + "_encrypted.anim");
        AssetDatabase.CopyAsset(dir, output);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return AssetDatabase.LoadAssetAtPath(output, typeof(RuntimeAnimatorController)) as AnimatorController;
    }

    public static void DuplicateAniamtions(string animation_dir, string new_dir, GameObject[] objs)
    {
        string[] files = Directory.GetFiles(animation_dir);
        foreach (string file in files)
        {
            string filename = Path.GetFileName(file);
            if (!filename.Contains(".anim"))
                continue;
            if (filename.Contains("dummy"))
                continue;
            File.Copy(file, Path.Combine(new_dir, filename), true);

            string path = Path.Combine(new_dir, filename);
            string anim = File.ReadAllText(path);

            Match match = Regex.Match(filename, "key(\\d+).*?\\.anim");
            int n = 0;
            if (match.Success)
                n = int.Parse(match.Groups[1].ToString());

            foreach (var obj in objs)
            {
                if (obj.name == "Body")
                    continue;

                string hr_path = obj.transform.GetHierarchyPath();
                hr_path = Regex.Replace(hr_path, ".*?/(.*)", "$1");

                string curve;

                if (!filename.Contains("_"))
                    curve = Regex.Replace(curve1, "attribute: material._Key\\d+", "attribute: material._Key" + n);
                else
                    curve = Regex.Replace(curve2, "attribute: material._Key\\d+", "attribute: material._Key" + n);
                curve = Regex.Replace(curve, "path: Body", "path: " + hr_path);
                if (obj.GetComponent<SkinnedMeshRenderer>() == null)
                    curve = Regex.Replace(curve, "classID: 137", "classID: 23");

                anim = Regex.Replace(anim, "m_FloatCurves:", "m_FloatCurves:" + curve);
                anim = Regex.Replace(anim, "m_EditorCurves:", "m_EditorCurves:" + curve);
            }
            File.WriteAllText(Path.Combine(new_dir, filename), anim);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static BlendTree[] CreateKeyTree(string animation_dir, int key_length, float speed)
    {
        BlendTree[] tree = new BlendTree[key_length];
        for (int i = 0; i < key_length; ++i)
        {
            BlendTree tree_key = new BlendTree();
            tree_key.name = "key" + i;
            tree_key.blendType = BlendTreeType.Direct;
            tree_key.blendParameter = "key_weight";
            tree_key.blendType = BlendTreeType.Simple1D;
            tree_key.blendParameter = "pkey" + i;
            tree_key.useAutomaticThresholds = false;

            Motion motion0 = AssetDatabase.LoadAssetAtPath(Path.Combine(animation_dir, "key" + i + ".anim"), typeof(AnimationClip)) as AnimationClip;
            Motion motion1 = AssetDatabase.LoadAssetAtPath(Path.Combine(animation_dir, "key" + i + "_2.anim"), typeof(AnimationClip)) as AnimationClip;

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

    private static void AddTransition(AnimatorStateTransition transition, int key_length, int idx)
    {
        transition.AddCondition(AnimatorConditionMode.IfNot, 0, "encrypt_lock");
        if (key_length == 4)
        {
            int n = 2;
            AnimatorConditionMode[] mode = new AnimatorConditionMode[n];
            for (int i = 0; i < n; ++i)
                mode[i] = AnimatorConditionMode.IfNot;
            if ((idx & 0b0001) == 1)
                mode[0] = AnimatorConditionMode.If;
            if ((idx & 0b0010) == 2)
                mode[1] = AnimatorConditionMode.If;
            for (int i = 0; i < n; ++i)
                transition.AddCondition(mode[i], 0, "encrypt_switch" + i);
        }
        else if(key_length == 8)
        {
            int n = 3;
            AnimatorConditionMode[] mode = new AnimatorConditionMode[n];
            for (int i = 0; i < n; ++i)
                mode[i] = AnimatorConditionMode.IfNot;
            if ((idx & 0b0001) == 1)
                mode[0] = AnimatorConditionMode.If;
            if ((idx & 0b0010) == 2)
                mode[1] = AnimatorConditionMode.If;
            if ((idx & 0b0100) == 4)
                mode[2] = AnimatorConditionMode.If;
            for (int i = 0; i < n; ++i)
                transition.AddCondition(mode[i], 0, "encrypt_switch" + i);
        }
        else
        {
            int n = 4;
            AnimatorConditionMode[] mode = new AnimatorConditionMode[n];
            for(int i = 0; i < n; ++i)
                mode[i] = AnimatorConditionMode.IfNot;
            if ((idx & 0b0001) == 1)
                mode[0] = AnimatorConditionMode.If;
            if ((idx & 0b0010) == 2)
                mode[1] = AnimatorConditionMode.If;
            if ((idx & 0b0100) == 4)
                mode[2] = AnimatorConditionMode.If;
            if ((idx & 0b1000) == 8)
                mode[3] = AnimatorConditionMode.If;
            for(int i = 0; i < n; ++i)
                transition.AddCondition(mode[i], 0, "encrypt_switch" + i);
        }
    }
    public static void AddParameter(AnimatorController anim, int key_length, bool optimize)
    {
        var paramters = anim.parameters;
        for(int i = 0; i < paramters.Length; ++i)
        {
            string name = paramters[i].name;
            if (name == "key_weight")
                return;
        }
        anim.AddParameter(new AnimatorControllerParameter() { defaultFloat = 1.0f, name = "key_weight", type = AnimatorControllerParameterType.Float });
        
        for (int i = 0; i < key_length; ++i)
            anim.AddParameter("pkey" + i, AnimatorControllerParameterType.Float);

        if(optimize)
        {
            anim.AddParameter("pkey", AnimatorControllerParameterType.Float);
            anim.AddParameter("encrypt_lock", AnimatorControllerParameterType.Bool);
            int switch_count = 1;
            switch(key_length)
            {
                case 4:
                    switch_count = 2;
                    break;
                case 8:
                    switch_count = 3;
                    break;
                case 12:
                case 16:
                    switch_count = 4;
                    break;
                default:
                    Debug.LogErrorFormat("AnimatorManager-AddParameter: key_length = {} is wrong!", key_length);
                    return;
            }

            for (int i = 0; i < switch_count; ++i)
                anim.AddParameter("encrypt_switch" + i, AnimatorControllerParameterType.Bool);
        }
    }
    public static void AddKeyLayer(AnimatorController anim, string animation_dir, int key_length, float speed = 10.0f, bool optimize = false)
    {
        AddParameter(anim, key_length, optimize);

        if (optimize)
        {
            AddKeyLayerMultiplexing(anim, animation_dir, key_length, speed);
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
    public static void AddKeyLayerMultiplexing(AnimatorController anim, string animation_dir, int key_length, float speed = 10.0f)
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

        for (int i = 0; i < key_length; ++i)
        {
            var key_state = layer.stateMachine.AddState("key" + i);

            var behaviour = key_state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            var behaviour_param = new VRCAvatarParameterDriver.Parameter
            {
                type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy,
                name = "pkey" + i,
                source = "pkey",
            };
            behaviour.parameters.Add(behaviour_param);

            transition = layer.stateMachine.AddAnyStateTransition(key_state);
            transition.canTransitionToSelf = false;
            transition.exitTime = 0;
            transition.duration = 0;
            transition.hasExitTime = false;
            AddTransition(transition, key_length, i);
        }

        AddKeyLayer(anim, animation_dir, key_length, speed, false);
    }

    public static bool IsMaterialInClip(AnimationClip clip, Material originalMaterial)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (EditorCurveBinding binding in bindings)
        {
            if ((binding.type == typeof(Renderer) || binding.type == typeof(SkinnedMeshRenderer)) && binding.propertyName.StartsWith("m_Materials"))
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
            if ((binding.type == typeof(Renderer) || binding.type == typeof(SkinnedMeshRenderer)) && binding.propertyName.StartsWith("m_Materials"))
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

    private static void SearchStateMachine(AnimatorStateMachine stateMachine, Material targetMaterial, Material encrypted, bool clone, string clonePath)
    {

        foreach (ChildAnimatorState state in stateMachine.states)
        {
            SearchMotion(state.state.motion, targetMaterial, encrypted, clone, clonePath);
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
        {
            SearchStateMachine(childStateMachine.stateMachine, targetMaterial, encrypted, clone, clonePath);
        }
    }

    private static void SearchMotion(Motion motion, Material targetMaterial, Material encrypted, bool clone, string clonePath)
    {
        if (motion is AnimationClip clip)
        {
            if (IsMaterialInClip(clip, targetMaterial))
            {
                Debug.LogFormat("{0} in {1}", targetMaterial.name, clip.name);
                if(clone)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    string copyPath = Path.Combine(clonePath, clip.name + ".anim");
                    if (!AssetDatabase.CopyAsset(path, copyPath))
                    {
                        Debug.LogError("Copy error: " + copyPath);
                        return;
                    }
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(copyPath);
                }
                ChangeMaterialInClip(clip, targetMaterial, encrypted);
            }
        }
        else if (motion is BlendTree blendTree)
        {
            foreach (ChildMotion childMotion in blendTree.children)
            {
                SearchMotion(childMotion.motion, targetMaterial, encrypted, clone, clonePath);
            }
        }
    }

    private static void ChangeMaterialInClip(AnimationClip clip, Material source, Material target, bool clone, string clonePath)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (EditorCurveBinding binding in bindings)
        {
            if ((binding.type == typeof(Renderer) || binding.type == typeof(SkinnedMeshRenderer)) && binding.propertyName.StartsWith("m_Materials"))
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

    public static void ChangeAnimationMaterial(AnimatorController anim, Material original, Material encrypted, bool clone, string clonePath)
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
    }
}
#endif