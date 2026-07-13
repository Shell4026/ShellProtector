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
    public class PipelineTests
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
            TestAssetScope.DeleteDefaultGeneratedRoot();
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
            AssertGeneratedPaths(encryptedAvatar, fixture.Material, TestAssetScope.GeneratedRoot);
            AssertFxController(encryptedAvatar);
            AssertExpressionParameters(encryptedAvatar);
            AssertBlendShapeWasObfuscated(encryptedAvatar);
            AssertAnimationMaterialWasRewritten(encryptedAvatar, fixture.Material);
        }

        [Test]
        public void ManualEncrypt_DuplicatesNonFxControllerBeforeObfuscatingBlendShapes()
        {
            Fixture fixture = CreateFixture("ControllerIsolation");
            VRCAvatarDescriptor originalDescriptor = fixture.Avatar.GetComponent<VRCAvatarDescriptor>();
            AnimationClip originalClip = CreateBlendShapeClip("ControllerIsolation");
            AnimatorController originalController = CreateBlendShapeController("ControllerIsolation", originalClip);
            originalDescriptor.baseAnimationLayers[0] = new VRCAvatarDescriptor.CustomAnimLayer
            {
                type = VRCAvatarDescriptor.AnimLayerType.Base,
                isDefault = false,
                animatorController = originalController
            };

            GameObject encryptedAvatar = fixture.Protector.Encrypt(false);
            sceneObjects.Add(encryptedAvatar);

            AnimatorController sourceController = originalDescriptor.baseAnimationLayers[0].animatorController as AnimatorController;
            AnimatorController encryptedController = encryptedAvatar.GetComponent<VRCAvatarDescriptor>()
                .baseAnimationLayers[0].animatorController as AnimatorController;
            AnimationClip sourceClip = GetFirstClip(sourceController);
            AnimationClip encryptedClip = GetFirstClip(encryptedController);

            Assert.That(sourceController, Is.SameAs(originalController));
            Assert.That(encryptedController, Is.Not.SameAs(originalController));
            Assert.That(sourceClip, Is.SameAs(originalClip));
            Assert.That(encryptedClip, Is.Not.SameAs(originalClip));
            Assert.That(AnimationUtility.GetCurveBindings(sourceClip).Single().propertyName, Is.EqualTo("blendShape.Smile"));
            Assert.That(AnimationUtility.GetCurveBindings(encryptedClip).Single().propertyName, Does.StartWith("blendShape."));
            Assert.That(AnimationUtility.GetCurveBindings(encryptedClip).Single().propertyName, Is.Not.EqualTo("blendShape.Smile"));
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
            AssertGeneratedPaths(avatar, fixture.Material, TestAssetScope.GeneratedRoot);
            AssertFxController(avatar);
            AssertExpressionParameters(avatar);
            AssertBlendShapeWasObfuscated(avatar);
            AssertAnimationMaterialWasRewritten(avatar, fixture.Material);
        }

        [Test]
        public void DefaultAssetDir_UsesGeneratedRootAndFolderGuids()
        {
            Fixture fixture = CreateFixture("Default", null);

            GameObject encryptedAvatar = fixture.Protector.Encrypt(false);
            sceneObjects.Add(encryptedAvatar);

            string avatarName = encryptedAvatar.name.Replace("_encrypted", "");
            AssertGeneratedPaths(encryptedAvatar, fixture.Material, TestAssetScope.DefaultGeneratedRoot);
            AssertOutputFoldersHaveGuids(TestAssetScope.DefaultGeneratedRoot, avatarName);
        }

        [Test]
        public void AddKeyLayer_DoesNotDuplicateShellProtectorParametersOrLayers()
        {
            string controllerDir = TestAssetScope.GeneratedRoot + "/Repeat";
            TestAssetScope.EnsureFolder(controllerDir);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerDir + "/fx.controller");
            string animationDir = "Assets/ShellProtector/Runtime/Animations";

            AnimatorManager.AddKeyLayer(controller, animationDir, 4, 1, 3.0f);
            AnimatorManager.AddKeyLayer(controller, animationDir, 4, 1, 3.0f);

            Assert.That(controller.layers.Count(l => l.name == "ShellProtector"), Is.EqualTo(1));
            Assert.That(controller.parameters.Count(p => p.name == "key_weight"), Is.EqualTo(1));
            Assert.That(controller.parameters.Count(p => p.name == ParameterManager.GetKeyName(0)), Is.EqualTo(1));
            Assert.That(controller.parameters.Count(p => p.name == ParameterManager.GetSyncLockName(true)), Is.EqualTo(1));
            Assert.That(controller.parameters.Count(p => p.name == ParameterManager.GetSyncSwitchName(0, true)), Is.EqualTo(1));
        }

        private Fixture CreateFixture(string name, string assetDir = TestAssetScope.GeneratedRoot)
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
            protector.Descriptor = descriptor;
            if (assetDir != null)
                protector.AssetDir = assetDir;
            SetSerializedField(protector, "_gameObjectList", new List<GameObject> { avatar });
            SetSerializedField(protector, "_algorithm", 1);
            SetSerializedField(protector, "_filter", 0);
            SetSerializedField(protector, "_fallback", 5);
            SetSerializedField(protector, "_keySize", 12);
            SetSerializedField(protector, "_syncSize", 1);
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

        private static AnimationClip CreateBlendShapeClip(string name)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = name + "BlendShape";
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = "Body",
                type = typeof(SkinnedMeshRenderer),
                propertyName = "blendShape.Smile"
            };
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f, 100f));
            string path = TestAssetScope.CreateAsset(clip, name + "/blendShape.anim");
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        private static AnimatorController CreateBlendShapeController(string name, AnimationClip clip)
        {
            string path = TestAssetScope.GeneratedRoot + "/" + name + "/base.controller";
            TestAssetScope.EnsureFolder(TestAssetScope.GeneratedRoot + "/" + name);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorState state = layer.stateMachine.AddState("BlendShape");
            state.motion = clip;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        private static AnimationClip GetFirstClip(AnimatorController controller)
        {
            return controller.layers[0].stateMachine.states[0].state.motion as AnimationClip;
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

        private static void AssertGeneratedPaths(GameObject avatar, Material originalMaterial, string expectedRoot)
        {
            string avatarRoot = expectedRoot + "/" + avatar.name.Replace("_encrypted", "");
            Material encryptedMaterial = avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>().sharedMaterial;
            Texture2D encryptedTexture = encryptedMaterial.GetTexture("_EncryptTex0") as Texture2D;
            Texture2D mipTexture = encryptedMaterial.GetTexture("_MipTex") as Texture2D;
            string materialPath = AssetDatabase.GetAssetPath(encryptedMaterial).Replace('\\', '/');
            string texturePath = AssetDatabase.GetAssetPath(encryptedTexture).Replace('\\', '/');
            string mipPath = AssetDatabase.GetAssetPath(mipTexture).Replace('\\', '/');

            Assert.That(materialPath, Does.StartWith(avatarRoot + "/Mat/"));
            Assert.That(texturePath, Does.StartWith(avatarRoot + "/Tex/"));
            Assert.That(mipPath, Does.StartWith(avatarRoot + "/Tex/"));
            Assert.That(materialPath, Does.Contain(originalMaterial.name));
            Assert.That(materialPath, Does.Not.Contain(originalMaterial.GetInstanceID().ToString()));
            Assert.That(materialPath, Does.Not.StartWith("Assets/ShellProtector/Runtime/"));
        }

        private static void AssertOutputFoldersHaveGuids(string root, string avatarName)
        {
            string avatarRoot = root + "/" + avatarName;
            foreach (string folderName in new[] { "Tex", "Mat", "Shader", "Anim", "Mesh" })
            {
                string folderPath = avatarRoot + "/" + folderName;
                string guid = AssetDatabase.AssetPathToGUID(folderPath);
                Assert.That(guid, Is.Not.Empty, folderPath);
                Assert.That(AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/'), Is.EqualTo(folderPath));
            }
        }

        private static void AssertFxController(GameObject avatar)
        {
            AnimatorController fx = ShellProtector.GetFx(avatar);

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
            AnimatorController fx = ShellProtector.GetFx(avatar);
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
