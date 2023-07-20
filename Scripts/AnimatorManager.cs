#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

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
            if(match.Success)
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

    public static void AddKeyLayer(AnimatorController anim, string animation_dir, int key_length, float speed = 10.0f)
    {
        anim.AddParameter(new AnimatorControllerParameter() { defaultFloat = 1.0f, name = "key_weight", type = AnimatorControllerParameterType.Float });
        AnimatorStateMachine stateMachine = new AnimatorStateMachine
        {
            name = anim.MakeUniqueLayerName("ShellProtector"),
            hideFlags = HideFlags.HideInHierarchy
        };

        anim.AddLayer(new AnimatorControllerLayer { name = stateMachine.name, defaultWeight = 1.0f, stateMachine = stateMachine });

        var layer = anim.layers[anim.layers.Length - 1];
        var state = layer.stateMachine.AddState("keys");

        BlendTree tree_root = new BlendTree();
        tree_root.name = "key_root";
        tree_root.blendType = BlendTreeType.Direct;
        tree_root.blendParameter = "key_weight";

        state.motion = tree_root;

        AssetDatabase.AddObjectToAsset(stateMachine, anim);
        AssetDatabase.AddObjectToAsset(state, anim);
        AssetDatabase.AddObjectToAsset(tree_root, anim);
        for (int i = 0; i < key_length; ++i)
        {
            anim.AddParameter("key" + i, AnimatorControllerParameterType.Float);

            BlendTree tree_key = new BlendTree();
            tree_key.name = "key" + i;
            tree_key.blendType = BlendTreeType.Direct;
            tree_key.blendParameter = "key_weight";
            tree_key.blendType = BlendTreeType.Simple1D;
            tree_key.blendParameter = "key" + i;
            tree_key.useAutomaticThresholds = false;
            
            Motion motion0 = AssetDatabase.LoadAssetAtPath(Path.Combine(animation_dir, "key" + i + ".anim"), typeof(AnimationClip)) as AnimationClip;
            Motion motion1 = AssetDatabase.LoadAssetAtPath(Path.Combine(animation_dir, "key" + i + "_2.anim"), typeof(AnimationClip)) as AnimationClip;

            tree_key.AddChild(motion0, -1);
            tree_key.AddChild(motion1, 1);

            ChildMotion[] motions = tree_key.children;
            for (int j = 0; j < motions.Length; ++j)
                motions[j].timeScale = speed;
            tree_key.children = motions;

            tree_root.AddChild(tree_key);

            AssetDatabase.AddObjectToAsset(tree_key, anim);
        }
        ChildMotion[] children = tree_root.children;
        for (int i = 0; i < children.Length; ++i)
            children[i].directBlendParameter = "key_weight";

        tree_root.children = children;
    }
}
#endif