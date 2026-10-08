#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Shell.Protector
{
    // A text file a user attaches to a bug report: the environment, the avatar, the settings, the key and every
    // material and texture the next build would encrypt. It is written in English for the developer.
    internal static class BugReport
    {
        static readonly AssetManager assetManager = AssetManager.GetInstance();
        static readonly string[] packagePrefixes = { "com.vrchat.", "nadena.dev.", "jp.lilxyzw.liltoon", "shell.protector" };
        const int LogLines = 80;
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
            Section(report, "Avatar", () => AppendAvatar(report, protector));
            Section(report, "Settings", () => AppendSettings(report, protector, settings));
            Section(report, "Key", () => AppendKey(report, protector));
            Section(report, "Encryption targets", () => AppendTargets(report, protector, settings));
            Section(report, "Materials", () => AppendMaterials(report, protector, settings));
            Section(report, "Textures", () => AppendTextures(report, settings));
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

            report.AppendLine("Packages:");
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .Where(p => packagePrefixes.Any(prefix => p.name.StartsWith(prefix)))
                .OrderBy(p => p.name);
            foreach (var package in packages)
                report.AppendLine("  " + package.name + " " + package.version);
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
                report.AppendLine("  Main texture: " + TextureName(material.mainTexture));

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
                textures.Add(material.mainTexture);
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
