#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace Shell.Protector
{
    // A text file a user attaches to a bug report: the environment and the other build tools, the avatar, the settings,
    // the key, every material and texture the next build would encrypt, and the renderers its key animations must reach.
    // It is written in English for the developer.
    internal static class BugReport
    {
        static readonly AssetManager assetManager = AssetManager.GetInstance();
        const int LogLines = 80;
        const int AnimationLines = 200;
        const int LogTailBytes = 4 * 1024 * 1024;

        public static void Save(ShellProtector protector, IEnumerable<string> detectedShaders, string title)
        {
            string avatarName = protector.Descriptor != null ? protector.Descriptor.name : protector.name;
            string fileName = string.Format("ShellProtector_Report_{0}_{1:yyyyMMdd_HHmmss}.txt", OutputPaths.Sanitize(avatarName), DateTime.Now);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string path = EditorUtility.SaveFilePanel(title, desktop, fileName, "txt");
            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, Build(protector, detectedShaders), new UTF8Encoding(false));
            EditorUtility.RevealInFinder(path);
        }

        public static string Build(ShellProtector protector, IEnumerable<string> detectedShaders)
        {
            var report = new StringBuilder();
            report.AppendLine("ShellProtector bug report");
            report.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            report.AppendLine("WARNING: This report contains the key bytes of this avatar. Send it only to the ShellProtector developer.");

            BuildSettings settings = protector.CreateSettings();
            Section(report, "Environment", () => AppendEnvironment(report, detectedShaders));
            Section(report, "Build plugins", () => AppendBuildPlugins(report));
            Section(report, "NDMF passes", () => AppendNdmfPasses(report));
            Section(report, "Avatar", () => AppendAvatar(report, protector));
            Section(report, "Settings", () => AppendSettings(report, protector, settings));
            Section(report, "Key", () => AppendKey(report, protector));
            Section(report, "Encryption targets", () => AppendTargets(report, protector, settings));
            Section(report, "Materials", () => AppendMaterials(report, protector, settings));
            Section(report, "Textures", () => AppendTextures(report, settings));
            Section(report, "Key targets", () => AppendKeyTargets(report, protector));
            Section(report, "Material animations", () => AppendMaterialAnimations(report, protector));
            Section(report, "Recent log", () => AppendLog(report));
            return report.ToString();
        }

        // A section that fails still leaves the rest of the report.
        static void Section(StringBuilder report, string name, Action append)
        {
            report.AppendLine();
            report.AppendLine("== " + name + " ==");
            try
            {
                append();
            }
            catch (Exception e)
            {
                report.AppendLine("(failed: " + e + ")");
            }
        }

        static void AppendEnvironment(StringBuilder report, IEnumerable<string> detectedShaders)
        {
            report.AppendLine("ShellProtector: " + VersionManager.GetInstance().GetVersion() + " (latest: " + VersionManager.GetInstance().GetGithubVersion() + ")");
            report.AppendLine("Unity: " + Application.unityVersion);
            report.AppendLine("OS: " + SystemInfo.operatingSystem);
            report.AppendLine("GPU: " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ", " + SystemInfo.graphicsDeviceVersion + ")");
            report.AppendLine("Build target: " + EditorUserBuildSettings.activeBuildTarget);
            report.AppendLine("Detected: " + string.Join(", ", detectedShaders));

            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var defines = symbols.Split(';').Where(s => s == "MODULAR" || s == "LILTOON" || s.StartsWith("POIYOMI"));
            report.AppendLine("Defines: " + string.Join(", ", defines));

            report.AppendLine("Packages (except com.unity.*):");
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .Where(p => !p.name.StartsWith("com.unity."))
                .OrderBy(p => p.name);
            foreach (var package in packages)
                report.AppendLine("  " + package.name + " " + package.version);
        }

        // Other tools that change the avatar during the build. Tools installed under Assets aren't packages, so the plugins
        // are found by their attributes and interfaces. NDMF is read by name: this assembly doesn't reference it.
        static void AppendBuildPlugins(StringBuilder report)
        {
            var plugins = new List<string>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                IList<CustomAttributeData> attributes;
                try
                {
                    attributes = assembly.GetCustomAttributesData();
                }
                catch (Exception)
                {
                    continue;
                }
                foreach (CustomAttributeData attribute in attributes)
                {
                    if (attribute.AttributeType.FullName == "nadena.dev.ndmf.ExportsPlugin" && attribute.ConstructorArguments.Count > 0 &&
                        attribute.ConstructorArguments[0].Value is Type type)
                        plugins.Add(type.FullName + " (" + assembly.GetName().Name + ")");
                }
            }
            report.AppendLine("NDMF plugins:");
            foreach (string plugin in plugins.OrderBy(p => p))
                report.AppendLine("  " + plugin);

            // The SDK creates each callback the same way before every build to sort them by order.
            var callbacks = new List<(int order, string name)>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IVRCSDKPreprocessAvatarCallback>())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                    continue;
                int order = int.MinValue;
                try
                {
                    if (Activator.CreateInstance(type, true) is IVRCSDKPreprocessAvatarCallback callback)
                        order = callback.callbackOrder;
                }
                catch (Exception)
                {
                }
                callbacks.Add((order, type.FullName + " (" + type.Assembly.GetName().Name + ")"));
            }
            report.AppendLine("VRChat preprocess callbacks, in build order:");
            foreach (var (order, name) in callbacks.OrderBy(c => c.order).ThenBy(c => c.name))
                report.AppendLine("  " + (order == int.MinValue ? "?" : order.ToString()) + " " + name);
        }

        // The order NDMF runs its passes in on this platform, as its plugin sequence window shows it. The resolver is
        // internal, so it is read by reflection: a version of NDMF that changed it loses only this section.
        static void AppendNdmfPasses(StringBuilder report)
        {
            Type resolverType = Type.GetType("nadena.dev.ndmf.PluginResolver, nadena.dev.ndmf");
            if (resolverType == null)
            {
                report.AppendLine("NDMF isn't installed.");
                return;
            }

            // PluginResolver(platform = null, includeDisabled = false): the current platform, with disabled plugins kept
            // so that they can be marked. Disabled passes are removed after sorting, so the order doesn't change.
            ConstructorInfo constructor = resolverType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters().All(p => p.IsOptional));
            if (constructor == null)
            {
                report.AppendLine("(unknown NDMF version: no PluginResolver constructor without arguments)");
                return;
            }
            object[] arguments = constructor.GetParameters()
                .Select(p => p.Name == "includeDisabled" ? true : p.HasDefaultValue ? p.DefaultValue : null)
                .ToArray();
            object resolver = constructor.Invoke(arguments);

            if (!(Member(resolver, "Passes") is System.Collections.IEnumerable phases))
            {
                report.AppendLine("(unknown NDMF version: no PluginResolver.Passes)");
                return;
            }

            report.AppendLine("Plugin: pass, in build order. [skipped] = not for this platform, [disabled] = turned off in NDMF.");
            foreach (object phase in phases)
            {
                object buildPhase = Member(phase, "Item1");
                report.AppendLine((Member(buildPhase, "Name") ?? buildPhase) + ":");
                if (!(Member(phase, "Item2") is System.Collections.IEnumerable passes))
                    continue;

                int index = 0;
                foreach (object pass in passes)
                {
                    string plugin = Member(Member(pass, "Plugin"), "QualifiedName") as string ?? "?";
                    object instantiated = Member(pass, "InstantiatedPass");
                    string name = Member(instantiated, "DisplayName") as string ?? Member(instantiated, "QualifiedName") as string ?? "?";

                    var line = new StringBuilder();
                    line.Append("  ").Append(++index).Append(". ").Append(plugin);
                    if (plugin == typeof(ShellProtector).Assembly.GetType("NdmfPlugin")?.FullName)
                        line.Append(" (ShellProtector)");
                    line.Append(": ").Append(name);
                    if (Member(pass, "Skipped") is bool skipped && skipped)
                        line.Append(" [skipped]");
                    if (SessionState.GetBool("nadena.dev.ndmf.plugin-disabled." + plugin, false))
                        line.Append(" [disabled]");
                    AppendExtensions(line, "opens", Member(pass, "ActivatePlugins"));
                    AppendExtensions(line, "closes", Member(pass, "DeactivatePlugins"));
                    report.AppendLine(line.ToString());
                }
            }
        }

        // The extension contexts (animator services, AAO's mesh info and so on) that NDMF opens or closes before a pass.
        static void AppendExtensions(StringBuilder line, string verb, object extensions)
        {
            if (!(extensions is IEnumerable<Type> types))
                return;
            string[] names = types.Select(t => t.Name).ToArray();
            if (names.Length > 0)
                line.Append(" (").Append(verb).Append(' ').Append(string.Join(", ", names)).Append(')');
        }

        // A property or field of any visibility, also one implemented from an interface.
        static object Member(object target, string name)
        {
            if (target == null)
                return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                PropertyInfo property = type.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(target);
                FieldInfo field = type.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(target);
            }
            foreach (Type contract in target.GetType().GetInterfaces())
            {
                PropertyInfo property = contract.GetProperty(name);
                if (property != null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(target);
            }
            return null;
        }

        static void AppendAvatar(StringBuilder report, ShellProtector protector)
        {
            report.AppendLine("Component: " + HierarchyPath(protector.transform));
            VRCAvatarDescriptor descriptor = protector.Descriptor;
            if (descriptor == null)
            {
                report.AppendLine("Descriptor: none");
                return;
            }

            report.AppendLine("Descriptor: " + HierarchyPath(descriptor.transform) + (descriptor.gameObject.activeInHierarchy ? "" : " (inactive)"));
            report.AppendLine("Prefab instance: " + PrefabUtility.IsPartOfPrefabInstance(descriptor));

            report.AppendLine("Root components:");
            foreach (Component component in descriptor.GetComponents<Component>())
                report.AppendLine("  " + ComponentName(component));
            report.AppendLine("Components in the avatar (except UnityEngine):");
            var components = descriptor.GetComponentsInChildren<Component>(true)
                .Select(ComponentName)
                .Where(name => !name.StartsWith("UnityEngine."))
                .GroupBy(name => name)
                .OrderBy(group => group.Key);
            foreach (var group in components)
                report.AppendLine("  " + group.Key + " x" + group.Count());

            var parameters = descriptor.expressionParameters;
            if (parameters == null)
                report.AppendLine("Expression parameters: none");
            else
            {
                report.AppendLine("Expression parameters: " + AssetPath(parameters) + ", " + parameters.CalcTotalCost() + "/256 bits, " + parameters.parameters.Length + " parameters");
                var shellParameters = parameters.parameters.Where(p => p != null && p.name != null && p.name.StartsWith(UserKey.SaltParameterPrefix)).Select(p => p.name);
                report.AppendLine("Salt parameters: " + string.Join(", ", shellParameters));
            }

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type != VRCAvatarDescriptor.AnimLayerType.FX)
                    continue;
                var fx = layer.animatorController as UnityEditor.Animations.AnimatorController;
                if (layer.isDefault || fx == null)
                    report.AppendLine("FX: default");
                else
                    report.AppendLine("FX: " + AssetPath(fx) + ", " + fx.layers.Length + " layers, " + fx.parameters.Length + " parameters");
            }

            report.AppendLine("Renderers:");
            foreach (var renderer in descriptor.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))
                    continue;
                var line = new StringBuilder("  " + RelativePath(renderer.transform, descriptor.transform) + " [" + renderer.GetType().Name + "]");
                if (!renderer.gameObject.activeInHierarchy)
                    line.Append(" (inactive)");
                if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                    line.Append(" mesh " + skinned.sharedMesh.name + ", " + skinned.sharedMesh.blendShapeCount + " blendshapes");
                line.Append(" materials: " + string.Join(", ", renderer.sharedMaterials.Select(m => m != null ? m.name : "none")));
                report.AppendLine(line.ToString());
            }
        }

        static void AppendSettings(StringBuilder report, ShellProtector protector, BuildSettings settings)
        {
            report.AppendLine("Algorithm: " + (ShellProtectorAlgorithm)settings.Algorithm);
            report.AppendLine("Default filter: " + Option(ShellProtector.FilterStrings, settings.Filter));
            report.AppendLine("Default fallback: " + Option(ShellProtector.FallbackStrings, settings.Fallback));
            report.AppendLine("Sync speed: " + settings.SyncSize);
            report.AppendLine("Small mip texture: " + settings.UseSmallMipTexture);
            report.AppendLine("Unlit safety fallback: " + settings.TurnOnAllSafetyFallback);
            report.AppendLine("Preserve MMD blendshapes: " + settings.PreserveMMD);
            report.AppendLine("Clean the output folder: " + settings.DeleteFolders);
            report.AppendLine("Output folder: " + settings.AssetDir + " (" + protector.GetEncryptedFoldersCount() + " generated folders)");
            var history = AssetDatabase.LoadAssetAtPath<EncryptedHistory>(OutputPaths.HistoryPath(settings.AssetDir));
            report.AppendLine("Encrypted history: " + (history == null ? "none" : history.IsOutdatedFormat ? "outdated format" : "current format"));
            report.AppendLine("Language: " + settings.Language);
        }

        static void AppendKey(StringBuilder report, ShellProtector protector)
        {
            UserKey userKey = protector.GetUserKey();
            report.AppendLine("Key size: " + ShellProtector.KeySize);
            report.AppendLine("Password length: " + (protector.UserPassword ?? "").Length);
            report.AppendLine("Salt: " + userKey.Salt);
            report.AppendLine("Key bytes: " + string.Join(" ", protector.GetKeyBytes().Select(b => b.ToString("x2"))));
        }

        static void AppendTargets(StringBuilder report, ShellProtector protector, BuildSettings settings)
        {
            var serialized = new SerializedObject(protector);
            Transform root = protector.Descriptor != null ? protector.Descriptor.transform : null;

            report.AppendLine("Objects:");
            foreach (var target in Elements(serialized.FindProperty("gameObjectList")))
                report.AppendLine("  " + (target is GameObject go ? RelativePath(go.transform, root) : "none"));
            report.AppendLine("Materials:");
            foreach (var target in Elements(serialized.FindProperty("materialList")))
                report.AppendLine("  " + (target is Material material ? material.name + " (" + AssetPath(material) + ")" : "none"));
            report.AppendLine("Obfuscated meshes:");
            foreach (var renderer in settings.ObfuscationRenderers)
            {
                if (renderer == null)
                    report.AppendLine("  none");
                else
                    report.AppendLine("  " + RelativePath(renderer.transform, root) + ", " + (renderer.sharedMesh != null ? renderer.sharedMesh.blendShapeCount : 0) + " blendshapes");
            }
        }

        static void AppendMaterials(StringBuilder report, ShellProtector protector, BuildSettings settings)
        {
            if (settings.Materials.Count == 0)
            {
                report.AppendLine("(none)");
                return;
            }

            for (int i = 0; i < settings.Materials.Count; i++)
            {
                Material material = settings.Materials[i];
                if (material == null)
                    continue;
                settings.MaterialOptions.TryGetValue(material, out ShellProtector.MatOption option);

                report.AppendLine("[" + i + "] " + material.name + " (" + AssetPath(material) + ")");
                Shader shader = material.shader;
                report.AppendLine("  Shader: " + (shader != null ? shader.name + " (" + ShaderKind(material) + ")" : "none"));
                if (shader != null && assetManager.IsPoiyomi(shader))
                    report.AppendLine("  Reused injected shader: " + (protector.IsEncryptedBefore(shader) != null));
                if (option == null)
                    report.AppendLine("  Option: default (active)");
                else
                    report.AppendLine("  Option: active " + option.Active + ", filter " + Option(ShellProtector.FilterStrings, option.Filter) +
                        ", fallback " + Option(ShellProtector.FallbackStrings, option.Fallback) + ", emission mask " + option.EmissionMask);

                MaterialIssues.Issue issue = MaterialIssues.Check(material);
                report.AppendLine("  Issue: " + (issue == MaterialIssues.Issue.None ? "none" : MaterialIssues.Message(issue)));
                report.AppendLine("  Main texture: " + TextureName(MaterialIssues.MainTexture(material)));

                string[] maps = MaterialIssues.EmissionMaps(material);
                for (int slot = 0; slot < maps.Length; slot++)
                {
                    if (!material.HasProperty(maps[slot]))
                        continue;
                    Texture map = material.GetTexture(maps[slot]);
                    if (map == null)
                        continue;
                    bool selected = option != null && (option.EmissionMask & (1 << slot)) != 0;
                    report.AppendLine("  Emission " + slot + " " + maps[slot] + ": " + TextureName(map) + (selected ? ", selected" : "") +
                        (MaterialIssues.IsSupportedEmission(map) ? "" : ", unsupported"));
                }
            }
        }

        static void AppendTextures(StringBuilder report, BuildSettings settings)
        {
            var textures = new List<Texture>();
            foreach (Material material in settings.Materials)
            {
                if (material == null)
                    continue;
                textures.Add(MaterialIssues.MainTexture(material));
                foreach (string map in MaterialIssues.EmissionMaps(material))
                {
                    if (material.HasProperty(map))
                        textures.Add(material.GetTexture(map));
                }
            }

            foreach (Texture texture in textures.Where(t => t != null).Distinct())
            {
                string path = AssetPath(texture);
                var line = new StringBuilder(texture.name + " (" + path + ") " + texture.width + "x" + texture.height);
                if (texture is Texture2D texture2D)
                    line.Append(", " + texture2D.format + ", " + texture2D.mipmapCount + " mips, readable " + texture2D.isReadable);
                else
                    line.Append(", " + texture.GetType().Name);
                line.Append(", supported " + TextureEncryptManager.IsSupportedTexture(texture));

                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    line.Append("; importer: " + importer.textureType + ", compression " + importer.textureCompression +
                        ", crunched " + importer.crunchedCompression + ", max size " + importer.maxTextureSize +
                        ", sRGB " + importer.sRGBTexture + ", alpha " + importer.alphaSource + ", npot " + importer.npotScale);
                    var standalone = importer.GetPlatformTextureSettings("Standalone");
                    if (standalone.overridden)
                        line.Append(", Standalone override " + standalone.format + " " + standalone.maxTextureSize);
                }
                report.AppendLine(line.ToString());
            }
        }

        // The renderers the key animations write to (AnimatorManager.AddKeyCurve): every renderer with a material the build
        // encrypts, bound by its path. An earlier object with the same path takes the key instead. The paths are the ones
        // before the build; a tool that moves or renames the renderer later breaks the binding too.
        static void AppendKeyTargets(StringBuilder report, ShellProtector protector)
        {
            VRCAvatarDescriptor descriptor = protector.Descriptor;
            if (descriptor == null)
            {
                report.AppendLine("(no descriptor)");
                return;
            }

            HashSet<Material> encrypted = EncryptedMaterials(protector);
            foreach (Renderer renderer in KeyTargets(descriptor, encrypted))
            {
                string path = RelativePath(renderer.transform, descriptor.transform);
                var line = new StringBuilder("  " + path + " [" + renderer.GetType().Name + "]");
                if (!renderer.gameObject.activeInHierarchy)
                    line.Append(" (inactive)");
                Transform bound = renderer.transform == descriptor.transform ? descriptor.transform : FindFirst(descriptor.transform, path);
                if (bound != renderer.transform)
                {
                    string components = bound != null ? string.Join(", ", bound.GetComponents<Component>().Select(ComponentName)) : "";
                    line.Append(", KEY BINDS TO ANOTHER OBJECT: " + (bound != null ? RelativePath(bound, descriptor.transform) + " (" + components + ")" : "none"));
                }
                line.Append(", slots: " + string.Join(", ", renderer.sharedMaterials.Select((m, i) => i + " " + MaterialName(m, encrypted, null))));
                report.AppendLine(line.ToString());
            }
        }

        // Animations that swap the materials of the key targets or swap in an encrypted material or one that uses a protected
        // texture, and the material properties the avatar's own animations set on the key targets.
        static void AppendMaterialAnimations(StringBuilder report, ShellProtector protector)
        {
            VRCAvatarDescriptor descriptor = protector.Descriptor;
            if (descriptor == null)
            {
                report.AppendLine("(no descriptor)");
                return;
            }

            HashSet<Material> encrypted = EncryptedMaterials(protector);
            var protectedTextures = new HashSet<Texture>(encrypted.Select(MaterialIssues.MainTexture).Where(t => t != null));
            var targetPaths = new HashSet<string>(KeyTargets(descriptor, encrypted).Select(r => RelativePath(r.transform, descriptor.transform)));

            var swaps = new List<string>();
            var properties = new SortedDictionary<string, SortedSet<string>>();
            foreach (var (label, controller, prefix) in Controllers(descriptor))
            {
                foreach (AnimationClip clip in controller.animationClips.Where(c => c != null).Distinct())
                {
                    foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    {
                        if (!typeof(Renderer).IsAssignableFrom(binding.type) || !binding.propertyName.StartsWith("m_Materials"))
                            continue;
                        string path = prefix + binding.path;
                        var materials = AnimationUtility.GetObjectReferenceCurve(clip, binding).Select(k => k.value as Material).Distinct().ToList();
                        bool relevant = targetPaths.Contains(path) ||
                            materials.Any(m => m != null && (encrypted.Contains(m) || UsesTexture(m, protectedTextures)));
                        if (relevant)
                            swaps.Add("  " + label + " / " + clip.name + ": " + path + " " + binding.propertyName + " -> " +
                                string.Join(", ", materials.Select(m => MaterialName(m, encrypted, protectedTextures))));
                    }

                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        string path = prefix + binding.path;
                        if (!typeof(Renderer).IsAssignableFrom(binding.type) || !binding.propertyName.StartsWith("material.") || !targetPaths.Contains(path))
                            continue;
                        if (!properties.TryGetValue(path, out SortedSet<string> names))
                            properties[path] = names = new SortedSet<string>();
                        names.Add(binding.propertyName.Substring("material.".Length).Split('.')[0]);
                    }
                }
            }

            report.AppendLine("Material swaps:");
            foreach (string swap in swaps.Take(AnimationLines))
                report.AppendLine(swap);
            if (swaps.Count > AnimationLines)
                report.AppendLine("  (" + (swaps.Count - AnimationLines) + " more)");
            report.AppendLine("Material properties animated on the key targets:");
            foreach (var pair in properties)
                report.AppendLine("  " + pair.Key + ": " + string.Join(", ", pair.Value));
        }

        // The playable layers and Modular Avatar's merged animators, with the prefix that makes their paths relative to the
        // avatar. A relative merge animator is taken to be relative to its own object (an empty relativePathRoot).
        // Modular Avatar is read by name: this assembly doesn't reference it.
        static IEnumerable<(string label, RuntimeAnimatorController controller, string prefix)> Controllers(VRCAvatarDescriptor descriptor)
        {
            var layers = (descriptor.baseAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>())
                .Concat(descriptor.specialAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>());
            foreach (var layer in layers)
            {
                if (!layer.isDefault && layer.animatorController != null)
                    yield return (layer.type + " " + layer.animatorController.name, layer.animatorController, "");
            }

            foreach (Component component in descriptor.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().FullName != "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator")
                    continue;
                Type type = component.GetType();
                if (!(type.GetField("animator")?.GetValue(component) is RuntimeAnimatorController controller))
                    continue;
                string objectPath = RelativePath(component.transform, descriptor.transform);
                bool relative = type.GetField("pathMode")?.GetValue(component)?.ToString() == "Relative";
                string prefix = relative && component.transform != descriptor.transform ? objectPath + "/" : "";
                yield return ("MA Merge Animator on " + objectPath + " " + controller.name, controller, prefix);
            }
        }

        // The materials the next build encrypts: active, and without an issue that makes the build skip them.
        static HashSet<Material> EncryptedMaterials(ShellProtector protector)
        {
            return new HashSet<Material>(protector.GetActiveMaterials()
                .Select(pair => pair.material)
                .Where(m => MaterialIssues.Check(m) == MaterialIssues.Issue.None));
        }

        static IEnumerable<Renderer> KeyTargets(VRCAvatarDescriptor descriptor, HashSet<Material> encrypted)
        {
            return descriptor.GetComponentsInChildren<Renderer>(true)
                .Where(r => (r is SkinnedMeshRenderer || r is MeshRenderer) && r.sharedMaterials.Any(m => m != null && encrypted.Contains(m)));
        }

        // The object an animation path binds to: the first child with each name, as Transform.Find takes it.
        static Transform FindFirst(Transform root, string path)
        {
            Transform current = root;
            foreach (string name in path.Split('/'))
            {
                Transform next = null;
                for (int i = 0; i < current.childCount && next == null; i++)
                {
                    if (current.GetChild(i).name == name)
                        next = current.GetChild(i);
                }
                if (next == null)
                    return null;
                current = next;
            }
            return current;
        }

        static bool UsesTexture(Material material, HashSet<Texture> textures)
        {
            return material.GetTexturePropertyNames().Any(name => textures.Contains(material.GetTexture(name)));
        }

        static string MaterialName(Material material, HashSet<Material> encrypted, HashSet<Texture> protectedTextures)
        {
            if (material == null)
                return "none";
            if (encrypted.Contains(material))
                return material.name + " (encrypted)";
            if (protectedTextures != null && UsesTexture(material, protectedTextures))
                return material.name + " (not encrypted, uses a protected texture)";
            return material.name;
        }

        static string ComponentName(Component component)
        {
            return component == null ? "(missing script)" : component.GetType().FullName;
        }

        // The ShellProtector lines and shader errors at the end of the editor log, read without loading the whole file.
        // Other exceptions are left out: some packages log one in every editor update.
        static void AppendLog(StringBuilder report)
        {
            string path = Application.consoleLogPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                report.AppendLine("(no editor log)");
                return;
            }

            string text;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long start = Math.Max(0, stream.Length - LogTailBytes);
                stream.Seek(start, SeekOrigin.Begin);
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    text = reader.ReadToEnd();
            }

            var lines = text.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.Contains("ShellProtector") || l.Contains("Shell.Protector") || l.Contains("Shader error"))
                .ToList();
            foreach (string line in lines.Skip(Math.Max(0, lines.Count - LogLines)))
                report.AppendLine(line);
        }

        static IEnumerable<UnityEngine.Object> Elements(SerializedProperty list)
        {
            if (list == null)
                yield break;
            for (int i = 0; i < list.arraySize; i++)
                yield return list.GetArrayElementAtIndex(i).objectReferenceValue;
        }

        static string ShaderKind(Material material)
        {
            if (assetManager.IsLockPoiyomi(material))
                return "locked Poiyomi";
            if (!assetManager.IsSupportShader(material.shader))
                return "unsupported";
            return assetManager.IsLilToon(material.shader) ? "lilToon" : "Poiyomi " + assetManager.GetShaderType(material.shader);
        }

        static string TextureName(Texture texture)
        {
            return texture == null ? "none" : texture.name + " (" + AssetPath(texture) + ")";
        }

        static string Option(string[] names, int index)
        {
            return index >= 0 && index < names.Length ? names[index] : "default";
        }

        static string AssetPath(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? "not an asset" : path;
        }

        static string HierarchyPath(Transform transform)
        {
            return RelativePath(transform, null);
        }

        static string RelativePath(Transform transform, Transform root)
        {
            var names = new List<string>();
            for (Transform t = transform; t != null && t != root; t = t.parent)
                names.Add(t.name);
            names.Reverse();
            return names.Count == 0 ? "(root)" : string.Join("/", names);
        }
    }
}
#endif
