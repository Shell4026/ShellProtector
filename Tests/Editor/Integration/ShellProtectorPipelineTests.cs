#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Shell.Protector;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Shell.Protector.Tests.Integration
{
    public class ShellProtectorPipelineTests
    {
        private readonly List<Object> sceneObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            TestAssetScope.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            TestAssetScope.DestroyObjects(sceneObjects);
            TestAssetScope.DeleteGeneratedRoot();
        }

        [Test]
        public void ManualEncrypt_CreatesEncryptedAvatarAndRewritesProtectionAssets()
        {
            Fixture fixture = CreateFixture("Manual");

            GameObject encryptedAvatar = fixture.Protector.Encrypt(false);
            sceneObjects.Add(encryptedAvatar);

            Assert.That(encryptedAvatar, Is.Not.Null);
            Assert.That(encryptedAvatar, Is.Not.SameAs(fixture.Avatar));
            Assert.That(encryptedAvatar.name, Does.Contain("_encrypted"));
            Assert.That(fixture.Avatar.activeSelf, Is.False);
            Assert.That(encryptedAvatar.GetComponentInChildren<ShellProtector>(true), Is.Null);
            Assert.That(encryptedAvatar.GetComponentInChildren<ShellProtectorTester>(true), Is.Not.Null);

            AssertEncryptedRenderer(encryptedAvatar, fixture.Material);
            AssertFxController(encryptedAvatar);
            AssertExpressionParameters(encryptedAvatar);
            AssertBlendShapeWasObfuscated(encryptedAvatar);
            AssertAnimationMaterialWasRewritten(encryptedAvatar, fixture.Material);
        }

        [Test]
        public void InPlaceEncrypt_RewritesOriginalAvatarWhenNdmfStyleStepsRun()
        {
            Fixture fixture = CreateFixture("InPlace");

            GameObject avatar = fixture.Protector.Encrypt(true);
            fixture.Protector.ReplaceMaterials(avatar);
            fixture.Protector.RemoveDuplicatedTextures(avatar);
            fixture.Protector.SetAnimations(avatar, false);
            fixture.Protector.ObfuscateBlendShape(avatar, false);
            fixture.Protector.ChangeMaterialsInAnims(avatar, false);
            fixture.Protector.CleanComponent(avatar);

            Assert.That(avatar, Is.SameAs(fixture.Avatar));
            Assert.That(avatar.activeSelf, Is.True);
            Assert.That(avatar.GetComponentInChildren<ShellProtector>(true), Is.Null);
            Assert.That(avatar.GetComponentInChildren<ShellProtectorTester>(true), Is.Null);

            AssertEncryptedRenderer(avatar, fixture.Material);
            AssertFxController(avatar);
            AssertExpressionParameters(avatar);
            AssertBlendShapeWasObfuscated(avatar);
            AssertAnimationMaterialWasRewritten(avatar, fixture.Material);
        }

        private Fixture CreateFixture(string name)
        {
            Texture2D texture = TestAssetScope.CreatePatternTexture(128, 128, TextureFormat.RGBA32, true);
            texture.name = name + "Texture";
            string texturePath = TestAssetScope.CreateAsset(texture, name + "/texture.asset");
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            Shader lilToon = AssetDatabase.LoadAssetAtPath<Shader>("Packages/jp.lilxyzw.liltoon/Shader/lts.shader");
            Assert.That(lilToon, Is.Not.Null, "lilToon lts.shader is required for the integration fixture.");

            Material material = new Material(lilToon);
            material.name = name + "Material";
            material.mainTexture = texture;
            string materialPath = TestAssetScope.CreateAsset(material, name + "/material.mat");
            material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            Mesh mesh = TestAssetScope.CreateBlendShapeQuadMesh();
            string meshPath = TestAssetScope.CreateAsset(mesh, name + "/mesh.asset");
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            AnimationClip materialClip = CreateMaterialClip(name, material);
            AnimatorController fx = CreateFxController(name, materialClip);
            ScriptableObject parameters = CreateExpressionParameters(name);

            GameObject avatar = new GameObject(name + "Avatar");
            sceneObjects.Add(avatar);

            GameObject body = new GameObject("Body");
            body.transform.SetParent(avatar.transform, false);
            SkinnedMeshRenderer renderer = body.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = material;

            VRCAvatarDescriptor descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
            VrcExpressionParametersTestUtil.SetDescriptorParameters(descriptor, parameters);
            descriptor.VisemeBlendShapes = Enumerable.Repeat(string.Empty, (int)VRC_AvatarDescriptor.Viseme.Count).ToArray();
            var eyeSettings = descriptor.customEyeLookSettings;
            eyeSettings.eyelidsBlendshapes = new int[0];
            descriptor.customEyeLookSettings = eyeSettings;
            descriptor.baseAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[5];
            descriptor.baseAnimationLayers[4] = new VRCAvatarDescriptor.CustomAnimLayer
            {
                type = VRCAvatarDescriptor.AnimLayerType.FX,
                isDefault = false,
                animatorController = fx
            };

            ShellProtector protector = avatar.AddComponent<ShellProtector>();
            protector.descriptor = descriptor;
            protector.assetDir = TestAssetScope.GeneratedRoot;
            SetSerializedField(protector, "gameobjectList", new List<GameObject> { avatar });
            SetSerializedField(protector, "algorithm", 1);
            SetSerializedField(protector, "filter", 0);
            SetSerializedField(protector, "fallback", 5);
            SetSerializedField(protector, "keySize", 12);
            SetSerializedField(protector, "syncSize", 1);
            protector.Init();

            return new Fixture
            {
                Avatar = avatar,
                Protector = protector,
                Material = material
            };
        }

        private static AnimationClip CreateMaterialClip(string name, Material material)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = name + "MaterialSwap";
            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("Body", typeof(SkinnedMeshRenderer), "m_Materials.Array.data[0]");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
            {
                new ObjectReferenceKeyframe { time = 0f, value = material }
            });
            string path = TestAssetScope.CreateAsset(clip, name + "/materialSwap.anim");
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        private static AnimatorController CreateFxController(string name, AnimationClip materialClip)
        {
            string path = TestAssetScope.GeneratedRoot + "/" + name + "/fx.controller";
            TestAssetScope.EnsureFolder(TestAssetScope.GeneratedRoot + "/" + name);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorState state = layer.stateMachine.AddState("MaterialSwap");
            state.motion = materialClip;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        private static ScriptableObject CreateExpressionParameters(string name)
        {
            ScriptableObject parameters = VrcExpressionParametersTestUtil.Create(
                name + "Params",
                new VrcExpressionParametersTestUtil.ParameterSpec
                {
                    Name = "existing",
                    Saved = false,
                    NetworkSynced = false,
                    ValueType = "Bool",
                    DefaultValue = 0f
                });
            string path = TestAssetScope.CreateAsset(parameters, name + "/params.asset");
            return AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        }

        private static void AssertEncryptedRenderer(GameObject avatar, Material originalMaterial)
        {
            SkinnedMeshRenderer renderer = avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            Material encryptedMaterial = renderer.sharedMaterial;

            Assert.That(encryptedMaterial, Is.Not.Null);
            Assert.That(encryptedMaterial, Is.Not.SameAs(originalMaterial));
            Assert.That(encryptedMaterial.name, Does.Contain("_encrypted"));
            Assert.That(encryptedMaterial.mainTexture, Is.Not.SameAs(originalMaterial.mainTexture));
            Assert.That(encryptedMaterial.GetTexture("_EncryptTex0"), Is.Not.Null);
            Assert.That(encryptedMaterial.GetTexture("_MipTex"), Is.Not.Null);
            Assert.That(encryptedMaterial.GetTag("VRCFallback", false), Is.EqualTo("Unlit"));
            Assert.That(encryptedMaterial.IsKeywordEnabled("_SHELL_PROTECTOR_CHACHA"), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(encryptedMaterial), Does.StartWith(TestAssetScope.GeneratedRoot));
        }

        private static void AssertFxController(GameObject avatar)
        {
            AnimatorController fx = ShellProtector.Getfx(avatar);

            Assert.That(fx, Is.Not.Null);
            Assert.That(fx.layers.Select(l => l.name), Does.Contain("ShellProtector"));
            Assert.That(fx.layers.Select(l => l.name), Does.Contain("ShellProtectorDemux"));
            Assert.That(fx.parameters.Select(p => p.name), Does.Contain("SHELL_PROTECTOR_key0"));
            Assert.That(fx.parameters.Select(p => p.name), Does.Contain("encrypt_lock"));
        }

        private static void AssertExpressionParameters(GameObject avatar)
        {
            VRCAvatarDescriptor descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            ScriptableObject parameters = VrcExpressionParametersTestUtil.GetDescriptorParameters(descriptor);
            VrcExpressionParametersTestUtil.ParameterSnapshot[] snapshots = VrcExpressionParametersTestUtil.Read(parameters).ToArray();

            Assert.That(parameters, Is.Not.Null);
            Assert.That(snapshots.Select(p => p.Name), Does.Contain("SHELL_PROTECTOR_key11"));
            Assert.That(snapshots.Select(p => p.Name), Does.Contain("encrypt_lock"));
            Assert.That(AssetDatabase.GetAssetPath(parameters), Does.StartWith(TestAssetScope.GeneratedRoot));
        }

        private static void AssertBlendShapeWasObfuscated(GameObject avatar)
        {
            SkinnedMeshRenderer renderer = avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();

            Assert.That(renderer.sharedMesh.blendShapeCount, Is.EqualTo(1));
            Assert.That(renderer.sharedMesh.GetBlendShapeName(0), Is.Not.EqualTo("Smile"));
            Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMesh), Does.StartWith(TestAssetScope.GeneratedRoot));
        }

        private static void AssertAnimationMaterialWasRewritten(GameObject avatar, Material originalMaterial)
        {
            AnimatorController fx = ShellProtector.Getfx(avatar);
            Material encryptedMaterial = avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>().sharedMaterial;
            List<AnimationClip> clips = new List<AnimationClip>();

            foreach (AnimatorControllerLayer layer in fx.layers)
                CollectClips(layer.stateMachine, clips);

            Assert.That(clips.Count, Is.GreaterThan(0));
            Assert.That(clips.Any(clip => AnimatorManager.IsMaterialInClip(clip, originalMaterial)), Is.False);
            Assert.That(clips.Any(clip => AnimatorManager.IsMaterialInClip(clip, encryptedMaterial)), Is.True);
        }

        private static void CollectClips(AnimatorStateMachine stateMachine, List<AnimationClip> clips)
        {
            foreach (ChildAnimatorState state in stateMachine.states)
            {
                if (state.state.motion is AnimationClip clip)
                    clips.Add(clip);
            }

            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
                CollectClips(child.stateMachine, clips);
        }

        private static void SetSerializedField<T>(ShellProtector protector, string fieldName, T value)
        {
            FieldInfo field = typeof(ShellProtector).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(protector, value);
        }

        private class Fixture
        {
            public GameObject Avatar;
            public ShellProtector Protector;
            public Material Material;
        }
    }
}
#endif
