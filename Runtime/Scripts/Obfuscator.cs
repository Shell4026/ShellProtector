#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Shell.Protector
{
    public class Obfuscator : ScriptableObject
    {
        string animDir = "";

        List<int> obfuscatedBlendShapeIndex = new List<int>();
        Dictionary<string, string> obfuscatedBlendShapeNames = new Dictionary<string, string>(); // before, after
        Dictionary<AnimationClip, AnimationClip> obfuscatedClip = new Dictionary<AnimationClip, AnimationClip>(); // before, after
        HashSet<string> mmdShapes = new HashSet<string>();

        readonly Regex re = new Regex(".*?/(.*)");

        public bool clone = true;
        public bool bPreserveMMD = true;

        public Obfuscator()
        {
            string[] shapes =
            {
                "まばたき", "笑い", "ウィンク", "ウィンク右", "ウィンク２", 
                "ｳｨﾝｸ２右", "なごみ", "はぅ", "びっくり", "じと目", "ｷﾘｯ", 
                "はちゅ目", "星目", "はぁと", "瞳小", "瞳大", "恐ろしい子！", 
                "ハイライト消し", "あ", "い", "えー", "う", "え", "お", "ワ", 
                "ω", "ω□", "∧", "▲", "はんっ！", "にやり", "にっこり", 
                "ぺろっ", "てへぺろ", "てへぺろ２", "口角上げ", "口角下げ", 
                "口横広げ", "真面目", "困る", "にこり", "怒り", "上", "下", 
                "頬染め", "がーん", "青ざめ", "涙", "ジト目", "△", "Λ", 
                "□", "にやり２", "照れ", "ん", "あ2", "白目", "あ２", "口角広げ"
            };
            foreach (string str in shapes) 
            {
                mmdShapes.Add(str);
            }
        }
        public void Clean()
        {
            clone = true;
            animDir = "";
            obfuscatedBlendShapeNames.Clear();
            obfuscatedBlendShapeIndex.Clear();
        }

        public Mesh ObfuscateBlendShapeMesh(Mesh mesh, string newPath)
        {
            Mesh obfuscatedMesh = Instantiate(mesh);
            obfuscatedMesh.ClearBlendShapes();

            for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                obfuscatedBlendShapeIndex.Add(shapeIndex);

            //shuffle
            var rng = new System.Random();
            int n = obfuscatedBlendShapeIndex.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                int value = obfuscatedBlendShapeIndex[k];
                obfuscatedBlendShapeIndex[k] = obfuscatedBlendShapeIndex[n];
                obfuscatedBlendShapeIndex[n] = value;
            }
            foreach (int shapeIndex in obfuscatedBlendShapeIndex)
            {
                for (var frameIndex = 0; frameIndex < mesh.GetBlendShapeFrameCount(shapeIndex); frameIndex++)
                {
                    Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
                    Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
                    Vector3[] deltaTangents = new Vector3[mesh.vertexCount];

                    mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);

                    float weight = mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                    string blendShapeName = mesh.GetBlendShapeName(shapeIndex);

                    if (bPreserveMMD)
                    {
                        if (mmdShapes.Contains(blendShapeName))
                        {
                            obfuscatedMesh.AddBlendShapeFrame(blendShapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                            continue;
                        }
                    }

                    if (obfuscatedBlendShapeNames.ContainsKey(blendShapeName))
                    {
                        blendShapeName = obfuscatedBlendShapeNames[blendShapeName];
                    }
                    else
                    {
                        string obfuscatedBlendShapeName = GUID.Generate().ToString();

                        obfuscatedBlendShapeNames.Add(blendShapeName, obfuscatedBlendShapeName);

                        blendShapeName = obfuscatedBlendShapeName;
                    }
                    obfuscatedMesh.AddBlendShapeFrame(blendShapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                }
            }
            Debug.LogFormat("Obfuscator blendshapes : {0}", string.Join(", ", obfuscatedBlendShapeNames.Select(kv => $"{kv.Key}: {kv.Value}")));

            AssetDatabase.CreateAsset(obfuscatedMesh, Path.Combine(newPath, obfuscatedMesh.name + obfuscatedMesh.GetHashCode() + ".asset"));
            AssetDatabase.Refresh();
            return obfuscatedMesh;
        }

        public void ChangeObfuscatedBlendShapeInDescriptor(VRCAvatarDescriptor descriptor)
        {
            for (int i = 0; i < descriptor.VisemeBlendShapes.Length; i++)
            {
                if (obfuscatedBlendShapeNames.ContainsKey(descriptor.VisemeBlendShapes[i]))
                    descriptor.VisemeBlendShapes[i] = obfuscatedBlendShapeNames[descriptor.VisemeBlendShapes[i]];
            }
            for (int i = 0; i < descriptor.customEyeLookSettings.eyelidsBlendshapes.Length; i++)
            {
                int idx = descriptor.customEyeLookSettings.eyelidsBlendshapes[i];
                descriptor.customEyeLookSettings.eyelidsBlendshapes[i] = obfuscatedBlendShapeIndex.FindIndex(0, obfuscatedBlendShapeIndex.Count - 1,
                    x =>
                    {
                        return x == idx;
                    }
                );
            }
            
        }

        public void ObfuscateBlendshapeInAnim(AnimatorController anim, GameObject obj, string newDir)
        {
            if (anim == null)
                return;

            animDir = newDir;

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
                SearchStateMachine(stateMachine, obj);
            }
        }

        void SearchStateMachine(AnimatorStateMachine stateMachine, GameObject obj)
        {
            for (int i = 0; i < stateMachine.states.Length; i++)
            {
                ChildAnimatorState state = stateMachine.states[i];
                AnimationClip clip = SearchMotion(state.state.motion, obj);
                if (clip != null)
                {
                    state.state.motion = clip;
                    stateMachine.states[i] = state;
                }
            }

            foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
            {
                SearchStateMachine(childStateMachine.stateMachine, obj);
            }
        }

        AnimationClip SearchMotion(Motion motion, GameObject obj)
        {
            if (motion is AnimationClip clip)
            {
                var tmp = ChangeBlendShapeInClip(clip, obj);
                if (clip == tmp)
                    return null;
                return tmp;
            }
            else if (motion is BlendTree blendTree)
            {
                for (int i = 0; i < blendTree.children.Length; ++i)
                {
                    ChildMotion childMotion = blendTree.children[i];
                    var result = SearchMotion(childMotion.motion, obj);
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
        public AnimationClip ChangeBlendShapeInClip(AnimationClip clip, GameObject obj)
        {
            bool detect = false;
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                {
                    Match m = re.Match(obj.transform.GetHierarchyPath());
                    string hierarchyPath = m.Groups[1].Value;
                    
                    if (binding.path != hierarchyPath)
                        continue;
                    string blendShapeName = binding.propertyName.Substring("blendShape.".Length);
                    if (obfuscatedBlendShapeNames.ContainsKey(blendShapeName))
                    {
                        Debug.LogFormat("find: {0},{1} in {2}", binding.path, blendShapeName, clip.name);
                        detect = true;
                        break;
                    }
                }
            }
            if (!detect)
            {
                return clip;
            }

            AnimationClip newClip = clip;
            if (clone)
            {
                if (obfuscatedClip.ContainsKey(clip))
                    newClip = obfuscatedClip[clip];
                else
                {
                    newClip = Instantiate(clip);
                    AssetDatabase.CreateAsset(newClip, Path.Combine(animDir, clip.name + clip.GetHashCode() + "_obfuscated.anim"));
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    obfuscatedClip.Add(clip, newClip);
                }
            }

            bindings = AnimationUtility.GetCurveBindings(newClip);
            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                {
                    Match m = re.Match(obj.transform.GetHierarchyPath());
                    string hierarchyPath = m.Groups[1].Value;

                    string blendShapeName = binding.propertyName.Substring("blendShape.".Length);
                    
                    if (binding.path != hierarchyPath)
                        continue;
                    if (!obfuscatedBlendShapeNames.ContainsKey(blendShapeName))
                        continue;
                    string newBlendShapeName = obfuscatedBlendShapeNames[blendShapeName];
                    EditorCurveBinding newBinding = new EditorCurveBinding
                    {
                        path = binding.path,
                        propertyName = $"blendShape.{newBlendShapeName}",
                        type = binding.type
                    };

                    AnimationCurve curve = AnimationUtility.GetEditorCurve(newClip, binding);
                    AnimationUtility.SetEditorCurve(newClip, binding, null);
                    AnimationUtility.SetEditorCurve(newClip, newBinding, curve);
                }
            }
            return newClip;
        }

        public IList<int> GetObfuscatedBlendShapeIndex()
        {
            return obfuscatedBlendShapeIndex.AsReadOnly();
        }

        public string GetOriginalBlendShapeName(string obfuscatedBlendShape)
        {
            if (!obfuscatedBlendShapeNames.ContainsKey(obfuscatedBlendShape))
                return null;
            return obfuscatedBlendShapeNames[obfuscatedBlendShape];
        }
    }
}
#endif