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
            SaltRegistry.FileOverride = null;
        }

        [Test]
        public void ManualEncrypt_CreatesEncryptedAvatarAndRewritesProtectionAssets()
        {
            Fixture fixture = CreateFixture("Manual");
            UserKey key = fixture.Protector.GetUserKey();

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
            AssertFxController(encryptedAvatar, key);
            AssertExpressionParameters(encryptedAvatar, key);
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
            UserKey key = fixture.Protector.GetUserKey();

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
            AssertFxController(avatar, key);
            AssertExpressionParameters(avatar, key);
            AssertBlendShapeWasObfuscated(avatar);
            AssertAnimationMaterialWasRewritten(avatar, fixture.Material);
        }

        // The face is a separate mesh whose shapes share names with the body's. Obfuscating the body must leave the
        // face's eyelid indices and viseme names alone, and obfuscating the face must remap them to the same shapes.
        [TestCase(false)]
        [TestCase(true)]
        public void ObfuscateBlendShape_RemapsDescriptorShapesOnlyForTheirOwnMesh(bool obfuscateFace)
        {
            const int shapeCount = 16;
            string name = obfuscateFace ? "FaceObfuscated" : "FaceKept";
            Fixture fixture = CreateFixture(name);
            SkinnedMeshRenderer body = fixture.Avatar.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            body.sharedMesh = CreateShapesMesh(name + "/body.asset", shapeCount);

            GameObject faceObject = new GameObject("Face");
            faceObject.transform.SetParent(fixture.Avatar.transform, false);
            SkinnedMeshRenderer face = faceObject.AddComponent<SkinnedMeshRenderer>();
            face.sharedMesh = CreateShapesMesh(name + "/face.asset", shapeCount);
            Mesh originalFaceMesh = face.sharedMesh;

            int[] eyelids = { 2, 5, -1 };
            VRCAvatarDescriptor descriptor = fixture.Avatar.GetComponent<VRCAvatarDescriptor>();
            descriptor.VisemeSkinnedMesh = face;
            descriptor.VisemeBlendShapes[0] = "Shape3";
            descriptor.VisemeBlendShapes[1] = "Shape7";
            descriptor.MouthOpenBlendShapeName = "Shape9";
            var eyeSettings = descriptor.customEyeLookSettings;
            eyeSettings.eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes;
            eyeSettings.eyelidsSkinnedMesh = face;
            eyeSettings.eyelidsBlendshapes = (int[])eyelids.Clone();
            descriptor.customEyeLookSettings = eyeSettings;

            // The face goes first, so the body's pass runs after the face's indices were already remapped.
            var renderers = obfuscateFace ? new List<SkinnedMeshRenderer> { face, body } : new List<SkinnedMeshRenderer> { body };
            SetSerializedField(fixture.Protector, "_obfuscationRenderers", renderers);

            fixture.Protector.ObfuscateBlendShape(fixture.Avatar, false);

            Assert.That(body.sharedMesh.GetBlendShapeName(0), Does.Not.StartWith("Shape"));
            if (obfuscateFace)
                Assert.That(face.sharedMesh, Is.Not.SameAs(originalFaceMesh));
            else
                Assert.That(face.sharedMesh, Is.SameAs(originalFaceMesh));

            int[] remapped = descriptor.customEyeLookSettings.eyelidsBlendshapes;
            Assert.That(remapped[2], Is.EqualTo(-1));
            for (int i = 0; i < 2; ++i)
            {
                Assert.That(remapped[i], Is.InRange(0, shapeCount - 1));
                Assert.That(GetShapeId(face.sharedMesh, remapped[i]), Is.EqualTo(eyelids[i]), "eyelid " + i);
            }

            Assert.That(GetShapeId(face.sharedMesh, face.sharedMesh.GetBlendShapeIndex(descriptor.VisemeBlendShapes[0])), Is.EqualTo(3));
            Assert.That(GetShapeId(face.sharedMesh, face.sharedMesh.GetBlendShapeIndex(descriptor.VisemeBlendShapes[1])), Is.EqualTo(7));
            Assert.That(GetShapeId(face.sharedMesh, face.sharedMesh.GetBlendShapeIndex(descriptor.MouthOpenBlendShapeName)), Is.EqualTo(9));
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
        public void Encrypt_DeletesOutputsFromOlderFormatVersion()
        {
            Fixture fixture = CreateFixture("OutdatedFormat");
            // A history saved before the format version existed deserializes as version 0.
            TestAssetScope.CreateAsset(ScriptableObject.CreateInstance<EncryptedHistory>(), "EncryptedHistory.asset");
            string staleFolder = TestAssetScope.GeneratedRoot + "/12345";
            TestAssetScope.EnsureFolder(staleFolder);

            GameObject encryptedAvatar = fixture.Protector.Encrypt(false);
            sceneObjects.Add(encryptedAvatar);

            Assert.That(AssetDatabase.IsValidFolder(staleFolder), Is.False);
            EncryptedHistory history = AssetDatabase.LoadAssetAtPath<EncryptedHistory>(TestAssetScope.GeneratedRoot + "/EncryptedHistory.asset");
            Assert.That(history, Is.Not.Null);
            Assert.That(history.IsOutdatedFormat, Is.False);
            AssertEncryptedRenderer(encryptedAvatar, fixture.Material);
        }

        [Test]
        public void Encrypt_KeepsOutputsFromCurrentFormatVersion()
        {
            Fixture fixture = CreateFixture("CurrentFormat");
            TestAssetScope.CreateAsset(EncryptedHistory.CreateCurrent(), "EncryptedHistory.asset");
            string previousFolder = TestAssetScope.GeneratedRoot + "/12345";
            TestAssetScope.EnsureFolder(previousFolder);

            GameObject encryptedAvatar = fixture.Protector.Encrypt(false);
            sceneObjects.Add(encryptedAvatar);

            Assert.That(AssetDatabase.IsValidFolder(previousFolder), Is.True);
        }

        [Test]
        public void AddKeyLayer_DoesNotDuplicateShellProtectorParametersOrLayers()
        {
            string controllerDir = TestAssetScope.GeneratedRoot + "/Repeat";
            TestAssetScope.EnsureFolder(controllerDir);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerDir + "/fx.controller");
            string animationDir = "Assets/ShellProtector/Runtime/Animations";

            UserKey key = TestKeys.UserKey;
            AnimatorManager.AddKeyLayer(controller, animationDir, 4, 1, 3.0f, key);
            AnimatorManager.AddKeyLayer(controller, animationDir, 4, 1, 3.0f, key);

            Assert.That(controller.layers.Count(l => l.name == "ShellProtector"), Is.EqualTo(1));
            Assert.That(controller.parameters.Count(p => p.name == "key_weight"), Is.EqualTo(1));
            Assert.That(controller.parameters.Count(p => p.name == ParameterManager.GetKeyName(0, key)), Is.EqualTo(1));
            Assert.That(controller.parameters.Count(p => p.name == ParameterManager.GetSyncLockName(1, key)), Is.EqualTo(1));
            Assert.That(controller.parameters.Count(p => p.name == ParameterManager.GetSyncSwitchName(0, 1, key)), Is.EqualTo(1));
        }

        [TestCase(8, 2)]
        [TestCase(4, 4)]
        public void AddKeyLayer_MuxSyncsSavedKeysAndKeepsCycling(int keyLength, int syncSize)
        {
            string controllerDir = TestAssetScope.GeneratedRoot + "/Mux";
            TestAssetScope.EnsureFolder(controllerDir);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerDir + "/fx.controller");
            UserKey key = TestKeys.UserKey;

            AnimatorManager.AddKeyLayer(controller, "Assets/ShellProtector/Runtime/Animations", keyLength, syncSize, 3.0f, key);

            AnimatorStateMachine mux = controller.layers.Single(l => l.name == "ShellProtectorMux").stateMachine;
            AnimatorState State(string name) => mux.states.Single(s => s.state.name == name).state;
            int steps = keyLength / syncSize;

            for (int step = 0; step < steps; ++step)
            {
                // The saved keys go into the synced keys, which the demux layer copies into the keys.
                var copies = State("mux" + step + "_sync").behaviours.OfType<VRCAvatarParameterDriver>()
                    .SelectMany(d => d.parameters).Where(p => p.type == VRC_AvatarParameterDriver.ChangeType.Copy).ToArray();
                Assert.That(copies.Select(p => p.source),
                    Is.EqualTo(Enumerable.Range(0, syncSize).Select(i => ParameterManager.GetSavedKeyName(step * syncSize + i, key))));
                Assert.That(copies.Select(p => p.name),
                    Is.EqualTo(Enumerable.Range(0, syncSize).Select(i => ParameterManager.GetSyncedKeyName(i, syncSize, key))));

                // The lock stays on while remote players' synced floats settle, and then stays off long enough to be synced.
                Assert.That(State("mux" + step + "_lock").transitions.Single().duration, Is.GreaterThan(0f));
                Assert.That(State("mux" + step + "_sync").transitions.Single().duration, Is.GreaterThan(0f));
            }

            Assert.That(State("mux" + (steps - 1) + "_unlock").transitions.Single().isExit, Is.True);

            AnimatorStateMachine demux = controller.layers.Single(l => l.name == "ShellProtectorDemux").stateMachine;
            var demuxCopies = demux.states.SelectMany(s => s.state.behaviours.OfType<VRCAvatarParameterDriver>()).SelectMany(d => d.parameters).ToArray();
            Assert.That(demuxCopies.Select(p => p.name), Is.EquivalentTo(Enumerable.Range(0, keyLength).Select(i => ParameterManager.GetKeyName(i, key))));
            Assert.That(controller.parameters.Select(p => p.name), Does.Contain(ParameterManager.GetSyncLockName(syncSize, key)));
            Assert.That(controller.parameters.Select(p => p.name), Does.Not.Contain(ParameterManager.GetSyncLockName(1, key)));
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

        // Shape i is named "Shape{i}" and moves every vertex by i + 1 along y, so GetShapeId finds it after renaming and shuffling.
        private static Mesh CreateShapesMesh(string path, int shapeCount)
        {
            Mesh mesh = TestAssetScope.CreateBlendShapeQuadMesh();
            mesh.ClearBlendShapes();
            Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
            Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
            Vector3[] deltaTangents = new Vector3[mesh.vertexCount];
            for (int shape = 0; shape < shapeCount; ++shape)
            {
                for (int v = 0; v < deltaVertices.Length; ++v)
                    deltaVertices[v] = new Vector3(0f, shape + 1, 0f);
                mesh.AddBlendShapeFrame("Shape" + shape, 100f, deltaVertices, deltaNormals, deltaTangents);
            }
            string assetPath = TestAssetScope.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        }

        private static int GetShapeId(Mesh mesh, int shapeIndex)
        {
            Assert.That(shapeIndex, Is.InRange(0, mesh.blendShapeCount - 1));
            Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(shapeIndex, 0, deltaVertices, null, null);
            return Mathf.RoundToInt(deltaVertices[0].y) - 1;
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

        private static void AssertFxController(GameObject avatar, UserKey key)
        {
            AnimatorController fx = ShellProtector.GetFx(avatar);

            Assert.That(fx, Is.Not.Null);
            Assert.That(fx.layers.Select(l => l.name), Does.Contain("ShellProtector"));
            Assert.That(fx.layers.Select(l => l.name), Does.Contain("ShellProtectorDemux"));
            Assert.That(fx.parameters.Select(p => p.name), Does.Contain(ParameterManager.GetKeyName(0, key)));
            Assert.That(fx.parameters.Select(p => p.name), Does.Contain(ParameterManager.GetSyncLockName(1, key)));
            Assert.That(fx.parameters.Any(p => p.name.StartsWith("SHELL_PROTECTOR_") || p.name == "encrypt_lock" || p.name == "pkey"), Is.False);
        }

        private static void AssertExpressionParameters(GameObject avatar, UserKey key)
        {
            VRCAvatarDescriptor descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            ScriptableObject parameters = VrcExpressionParametersTestUtil.GetDescriptorParameters(descriptor);
            VrcExpressionParametersTestUtil.ParameterSnapshot[] snapshots = VrcExpressionParametersTestUtil.Read(parameters).ToArray();

            Assert.That(parameters, Is.Not.Null);
            Assert.That(snapshots.Select(p => p.Name), Does.Contain(ParameterManager.GetKeyName(11, key)));
            Assert.That(snapshots.Select(p => p.Name), Does.Contain(ParameterManager.GetSyncLockName(1, key)));
            Assert.That(snapshots.Select(p => p.Name), Does.Contain(key.SaltParameterName));
            Assert.That(snapshots.Any(p => p.Name.StartsWith("SHELL_PROTECTOR_") || p.Name == "encrypt_lock" || p.Name == "pkey"), Is.False);
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
