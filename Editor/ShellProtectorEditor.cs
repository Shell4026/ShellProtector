#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using VRC.SDK3.Avatars.Components;

namespace Shell.Protector
{
    [CustomEditor(typeof(ShellProtector))]
    [CanEditMultipleObjects]
    public class ShellProtectorEditor : Editor
    {
        static readonly string[] languages = { "English", "한국어", "日本語" };
        static readonly string[] languageCodes = { "eng", "kor", "jp" };
        static readonly int[] syncSizes = { 1, 2, 4 };
        static readonly GUIContent[] syncSizeLabels = { new GUIContent("1"), new GUIContent("2"), new GUIContent("4") };

        // Foldout states are kept for the editor session, across every ShellProtector inspector.
        static bool advancedOption;
        static bool debug;

        ShellProtector root = null;
        readonly LanguageManager lang = LanguageManager.GetInstance();

        ReorderableList gameobjectList;
        ReorderableList materialList;
        ReorderableList textureList;
        ReorderableList obfuscationList;

        SerializedProperty descriptor;
        SerializedProperty languageIndex;
        SerializedProperty language;
        SerializedProperty userPassword;
        SerializedProperty filter;
        SerializedProperty fallback;
        SerializedProperty syncSize;
        SerializedProperty deleteFolders;
        SerializedProperty bUseSmallMipTexture;
        SerializedProperty bPreserveMMD;
        SerializedProperty turnOnAllSafetyFallback;
        ShellProtectorEditorViewModel viewModel;
        bool forceProgress = false;
        bool showPassword = false;

        string currentVersion = "";
        List<string> shaders = new List<string>();
        readonly List<Texture2D> debugTextures = new List<Texture2D>();
        List<string> materialWarnings = new List<string>();

        // Not serialized: Unity keeps an editor's serializable fields across script reloads, so the styles would never be rebuilt.
        [NonSerialized] GUIStyle titleStyle;
        [NonSerialized] GUIStyle versionStyle;
        [NonSerialized] GUIStyle newVersionStyle;
        [NonSerialized] GUIStyle sectionStyle;
        [NonSerialized] GUIStyle warningStyle;

        private string Lang(string word)
        {
            if (root == null)
                return "";
            return lang.GetLang(root.Language, word);
        }

        void OnEnable()
        {
            root = target as ShellProtector;

            gameobjectList = CreateList("gameObjectList", "Object list");
            materialList = CreateList("materialList", "Material List");
            obfuscationList = CreateList("obfuscationRenderers", "Obfuscated meshes");

            textureList = new ReorderableList(debugTextures, typeof(Texture2D), true, true, true, true);
            textureList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, Lang("Texture List"));
            textureList.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                debugTextures[index] = EditorGUI.ObjectField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), debugTextures[index], typeof(Texture2D), false) as Texture2D;
            };

            #region SerializedObject
            descriptor = serializedObject.FindProperty("descriptor");
            languageIndex = serializedObject.FindProperty("languageIndex");
            language = serializedObject.FindProperty("language");
            userPassword = serializedObject.FindProperty("userPassword");
            filter = serializedObject.FindProperty("filter");
            fallback = serializedObject.FindProperty("fallback");
            syncSize = serializedObject.FindProperty("syncSize");
            deleteFolders = serializedObject.FindProperty("deleteFolders");
            bUseSmallMipTexture = serializedObject.FindProperty("useSmallMipTexture");
            bPreserveMMD = serializedObject.FindProperty("preserveMmd");
            turnOnAllSafetyFallback = serializedObject.FindProperty("turnOnAllSafetyFallback");
            #endregion
            viewModel = new ShellProtectorEditorViewModel(root, syncSize, gameobjectList, materialList);

            foreach (var t in targets)
            {
                var protector = t as ShellProtector;
                if (protector == null)
                    continue;
                FindAvatar(protector);
                // Save the salt on the scene component so NDMF builds, which run on a copy, reuse it.
                protector.EnsureParameterSalt();
            }
            serializedObject.Update();
            MigrateAlgorithm();

            VersionManager.GetInstance().Refresh();
            currentVersion = VersionManager.GetInstance().GetVersion();

            shaders = AssetManager.GetInstance().CheckShader();
            AssetManager.GetInstance().CheckModular();
        }

        ReorderableList CreateList(string propertyName, string header)
        {
            var list = new ReorderableList(serializedObject, serializedObject.FindProperty(propertyName), true, true, true, true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, Lang(header));
            list.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y + 1, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };
            return list;
        }

        // A component added outside the avatar gets its avatar (and the Body target) once it is moved under one.
        static void FindAvatar(ShellProtector protector)
        {
            if (protector.Descriptor != null)
                return;
            var avatar = protector.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (avatar == null)
                return;

            protector.Descriptor = avatar;
            protector.Init();
            if (PrefabUtility.IsPartOfPrefabInstance(protector))
                PrefabUtility.RecordPrefabInstancePropertyModifications(protector);
            EditorUtility.SetDirty(protector);
        }

        // XXTEA is deprecated and can't be selected anymore, so components that still use it move to ChaCha.
        void MigrateAlgorithm()
        {
            SerializedProperty algorithm = serializedObject.FindProperty("algorithm");
            if (!algorithm.hasMultipleDifferentValues && algorithm.intValue == (int)ShellProtectorAlgorithm.Chacha)
                return;
            algorithm.intValue = (int)ShellProtectorAlgorithm.Chacha;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        void InitStyles()
        {
            if (titleStyle != null)
                return;
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 19, fixedHeight = 0 };
            // A fixed height keeps the version on the title's baseline without stretching the header row.
            versionStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerLeft, fixedHeight = titleStyle.CalcSize(new GUIContent("ShellProtector")).y };
            newVersionStyle = new GUIStyle(versionStyle) { fontStyle = FontStyle.Bold };
            sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, fixedHeight = 0 };
            warningStyle = new GUIStyle(EditorStyles.helpBox) { fontSize = EditorStyles.boldLabel.fontSize, fontStyle = FontStyle.Bold, wordWrap = true };
        }

        // Sections are told apart by their headers and the space between them, not by boxes.
        const float SectionSpacing = 20;

        void BeginSection(string title)
        {
            GUILayout.Label(title, sectionStyle);
            EditorGUILayout.Space(4);
        }

        static void EndSection()
        {
            EditorGUILayout.Space(SectionSpacing);
        }

        // EditorGUILayout.Foldout keeps a single-line rect whatever the font size, so a large title would miss its clicks.
        // This draws the standard arrow next to a label of any style and toggles on a click anywhere in the row.
        static bool Foldout(bool expanded, string title, GUIStyle style)
        {
            Rect row = GUILayoutUtility.GetRect(new GUIContent(title), style, GUILayout.ExpandWidth(true));
            const float arrowWidth = 14;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            Rect arrow = new Rect(row.x, row.y + (row.height - lineHeight) * 0.5f, arrowWidth, lineHeight);
            expanded = EditorGUI.Foldout(arrow, expanded, GUIContent.none, true);

            Rect label = row;
            label.xMin += arrowWidth;
            GUI.Label(label, title, style);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
            {
                expanded = !expanded;
                e.Use();
            }
            return expanded;
        }

        static void Hint(string text)
        {
            GUILayout.Label(text, EditorStyles.wordWrappedMiniLabel);
        }

        public override void OnInspectorGUI()
        {
            root = target as ShellProtector;
            InitStyles();
            serializedObject.Update();

            DrawHeader();
            DrawTargets();
            DrawObfuscation();
            DrawPassword();
            DrawOsc();
            DrawAdvancedOptions();
            DrawEncrypt();
            DrawDebug();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("ShellProtector", titleStyle);
            GUILayout.Label("v" + currentVersion, versionStyle);
            string latestVersion = VersionManager.GetInstance().GetGithubVersion();
            bool hasNewVersion = IsNewerVersion(latestVersion, currentVersion);
            if (!string.IsNullOrEmpty(latestVersion))
                GUILayout.Label(Lang("Latest: ") + "v" + latestVersion, hasNewVersion ? newVersionStyle : versionStyle);
            GUILayout.FlexibleSpace();
            int index = Mathf.Clamp(languageIndex.intValue, 0, languages.Length - 1);
            index = EditorGUILayout.Popup(index, languages, GUILayout.Width(90));
            if (languageIndex.intValue != index)
                languageIndex.intValue = index;
            if (language.stringValue != languageCodes[index])
                language.stringValue = languageCodes[index];
            EditorGUILayout.EndHorizontal();

            if (hasNewVersion)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(Lang("A new version is available: ") + latestVersion);
                if (GUILayout.Button(Lang("Releases page"), GUILayout.Width(110)))
                    Application.OpenURL(VersionManager.ReleasesUrl);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(descriptor, new GUIContent(Lang("Avatar")));
            if (EditorGUI.EndChangeCheck())
            {
                // The new avatar's Body becomes a target, as when the component is added.
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObjects(targets, "Change ShellProtector avatar");
                foreach (var t in targets)
                    (t as ShellProtector)?.Init();
                serializedObject.Update();
            }
            if (descriptor.objectReferenceValue == null && !descriptor.hasMultipleDifferentValues)
                EditorGUILayout.HelpBox(Lang("Assign the avatar to encrypt."), MessageType.Error);

            if (shaders.Count == 0)
                EditorGUILayout.HelpBox(Lang("No supported shader (lilToon, Poiyomi) was found in the project."), MessageType.Warning);
            EditorGUILayout.Space(SectionSpacing);
        }

        void DrawTargets()
        {
            BeginSection(Lang("Encryption targets"));
            gameobjectList.DoLayoutList();
            materialList.DoLayoutList();

            if (!viewModel.HasTargets)
            {
                EditorGUILayout.HelpBox(Lang("Add the objects or materials to encrypt."), MessageType.Warning);
                if (root.Descriptor != null && root.Descriptor.transform.Find("Body") != null && GUILayout.Button(Lang("Add Body")))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObjects(targets, "Add Body to ShellProtector");
                    foreach (var t in targets)
                        (t as ShellProtector)?.Init();
                    serializedObject.Update();
                }
            }
            else
                Hint(Lang("Encrypting too many objects can cause lag when loading avatars in-game."));

            if (GUILayout.Button(new GUIContent(Lang("Material advanced settings"), Lang("Filter, fallback and emission encryption per material."))))
                MaterialAdvancedSettings.ShowWindow(root);
            // Checked on layout only: the repaint that follows must draw the same boxes.
            if (Event.current.type == EventType.Layout)
                materialWarnings = CollectMaterialWarnings();
            foreach (string warning in materialWarnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            int emissionMaps = root.CountEncryptedEmissionMaps(out int emissionMaterials);
            if (emissionMaps > 0)
                Hint(string.Format(Lang("Emission encryption: {0} maps in {1} materials"), emissionMaps, emissionMaterials));
            EndSection();
        }

        // One warning per kind of problem in the materials to encrypt, naming the materials. The details are in the material settings.
        List<string> CollectMaterialWarnings()
        {
            var materials = new Dictionary<string, List<string>>();
            void Add(string warning, Material material)
            {
                if (!materials.TryGetValue(warning, out List<string> names))
                    materials.Add(warning, names = new List<string>());
                names.Add(material.name);
            }

            foreach (var (material, option) in root.GetActiveMaterials())
            {
                MaterialIssues.Issue issue = MaterialIssues.Check(material);
                if (issue != MaterialIssues.Issue.None)
                    Add(IssueWarning(issue), material);
                if (option != null && MaterialIssues.HasUnsupportedEmission(material, option.EmissionMask))
                    Add("There are emission maps that can't be encrypted.", material);
            }

            var warnings = new List<string>();
            foreach (var pair in materials)
                warnings.Add(Lang(pair.Key) + "\n" + string.Join(", ", pair.Value));
            return warnings;
        }

        static string IssueWarning(MaterialIssues.Issue issue)
        {
            switch (issue)
            {
                case MaterialIssues.Issue.UnsupportedShader: return "There are materials with an unsupported shader.";
                case MaterialIssues.Issue.EmptyMainTexture: return "There are materials without a main texture.";
                case MaterialIssues.Issue.MainTextureNotTexture2D: return "There are main textures that are not Texture2D.";
                case MaterialIssues.Issue.UnsupportedMainTextureFormat: return "There are textures in an unsupported format.";
                case MaterialIssues.Issue.OddMainTextureSize: return "There are textures whose size is not a multiple of 2.";
                default: return "";
            }
        }

        void DrawObfuscation()
        {
            BeginSection(Lang("BlendShape obfuscation"));
            obfuscationList.DoLayoutList();
            bPreserveMMD.boolValue = EditorGUILayout.Toggle(Lang("Preserve MMD BlendShapes"), bPreserveMMD.boolValue);
            EndSection();
        }

        void DrawPassword()
        {
            BeginSection(Lang("Password"));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(Lang("User password"));
            if (showPassword)
                userPassword.stringValue = GUILayout.TextField(userPassword.stringValue, ShellProtector.KeySize, EditorStyles.textField);
            else
                userPassword.stringValue = GUILayout.PasswordField(userPassword.stringValue, '*', ShellProtector.KeySize, EditorStyles.textField);
            showPassword = GUILayout.Toggle(showPassword, Lang("Show"), GUI.skin.button, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            int syncIndex = Math.Max(0, Array.IndexOf(syncSizes, syncSize.intValue));
            var syncLabel = new GUIContent(Lang("Sync speed"), Lang("Number of key bytes synced at once. At 2 or higher the key syncs faster and is saved in the avatar, so the OSC program only has to run once, but more parameters are used."));
            syncIndex = EditorGUILayout.Popup(syncLabel, syncIndex, syncSizeLabels);
            if (syncSize.intValue != syncSizes[syncIndex])
                syncSize.intValue = syncSizes[syncIndex];
            if (syncSize.intValue >= 2)
                Hint(Lang("The key is saved in the avatar, so the OSC program only has to run once. Uses more parameters."));
            else
                Hint(Lang("The OSC program must keep running while you play. Uses the fewest parameters."));

            viewModel.Refresh();
            if (!viewModel.HasParameterAsset)
                EditorGUILayout.HelpBox(Lang("Cannot find VRCExpressionParameters in your avatar!"), MessageType.Error);
            else
            {
                string usage = string.Format(Lang("Parameters: {0} bits used, {1} bits free"), viewModel.UsedParameter, viewModel.FreeParameter);
                if (viewModel.HasEnoughParameterSpace)
                    Hint(usage);
                else
                {
                    EditorGUILayout.HelpBox(Lang("Not enough parameter space!") + "\n" + usage, MessageType.Error);
                    forceProgress = EditorGUILayout.ToggleLeft(Lang("Force progress"), forceProgress);
                }
            }

            EndSection();
        }

        // Only the user password goes through the OSC program, and older OSC versions derive different keys.
        void DrawOsc()
        {
            BeginSection(Lang("OSC program"));

            // One label holds both the icon and the text: GUILayout can measure wrapped text in a horizontal group
            // differently per event, which shifts the click areas of everything below.
            var warning = new GUIContent(Lang("This version requires ShellProtectorOSC 1.7 or later. Older OSC versions can't unlock the avatar, so make sure to update the OSC program to the latest version."), EditorGUIUtility.IconContent("console.warnicon").image);
            GUILayout.Label(warning, warningStyle);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(OscDownloader.IsBusy))
            {
                Color background = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
                if (GUILayout.Button(Lang("Download latest OSC"), GUILayout.Height(26)))
                    OscDownloader.DownloadLatest(root.Language);
                GUI.backgroundColor = background;
            }
            if (GUILayout.Button(Lang("Releases page"), GUILayout.Width(110), GUILayout.Height(26)))
                Application.OpenURL(OscDownloader.ReleasesUrl);
            EditorGUILayout.EndHorizontal();
            if (syncSize.intValue >= 2)
                Hint(Lang("Run ShellProtectorOSC once while playing VRChat and enter the user password. The key stays saved in the avatar after that."));
            else
                Hint(Lang("Keep ShellProtectorOSC running while playing VRChat and enter the user password. Set the sync speed to 2 or higher to run it only once."));

            EndSection();
        }

        void DrawAdvancedOptions()
        {
            advancedOption = Foldout(advancedOption, Lang("Advanced options"), sectionStyle);
            if (!advancedOption)
            {
                EndSection();
                return;
            }
            EditorGUILayout.Space(4);

            GUILayout.Label(Lang("Texture"), EditorStyles.boldLabel);
            var filterLabel = new GUIContent(Lang("Default texture filter"), Lang("Setting it to 'Point' may result in aliasing, but performance is better."));
            filter.intValue = EditorGUILayout.Popup(filterLabel, filter.intValue, ToContents(ShellProtector.FilterStrings));
            var mipLabel = new GUIContent(Lang("Small mip texture"), Lang("It uses a smaller mipTexture to reduce memory usage and improve performance. It may look slightly different from the original when viewed from the side."));
            bUseSmallMipTexture.boolValue = EditorGUILayout.Toggle(mipLabel, bUseSmallMipTexture.boolValue);

            EditorGUILayout.Space(10);
            GUILayout.Label(Lang("Fallback Options"), EditorStyles.boldLabel);
            Hint(Lang("Opponents with Safety option turned on will see degraded textures instead of noise."));
            var unlitLabel = new GUIContent(Lang("Unlit safety fallback"), Lang("Change all Safety Fallback settings of shader to Unlit."));
            turnOnAllSafetyFallback.boolValue = EditorGUILayout.Toggle(unlitLabel, turnOnAllSafetyFallback.boolValue);
            fallback.intValue = EditorGUILayout.Popup(new GUIContent(Lang("Default fallback texture")), fallback.intValue, ToContents(ShellProtector.FallbackStrings));


            EditorGUILayout.Space(10);
            GUILayout.Label(Lang("Output"), EditorStyles.boldLabel);
            var deleteLabel = new GUIContent(Lang("Clean the output folder"), Lang("Delete folders that already exists when at creation time"));
            deleteFolders.boolValue = EditorGUILayout.Toggle(deleteLabel, deleteFolders.boolValue);
            if (GUILayout.Button(Lang("Delete previously encrypted files") + String.Format(" ({0})", root.GetEncryptedFoldersCount())))
            {
                serializedObject.ApplyModifiedProperties();
                root.CleanEncrypted();
                GUIUtility.ExitGUI();
            }
            EndSection();
        }

        bool CanEncrypt()
        {
            bool parameterReady = viewModel.HasEnoughParameterSpace || (viewModel.HasParameterAsset && forceProgress);
            return root.Descriptor != null && viewModel.HasTargets && parameterReady;
        }

        // With Modular Avatar the avatar is encrypted on upload, so manual encryption is only a test tool under Debug.
        void DrawEncrypt()
        {
#if MODULAR
            EditorGUILayout.HelpBox(Lang("Modular avatars exist. It is automatically encrypted on upload."), MessageType.Info);
#else
            using (new EditorGUI.DisabledScope(!CanEncrypt()))
            {
                Color background = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.55f, 0.85f, 0.6f);
                bool pressed = GUILayout.Button(Lang("Encrypt!"), GUILayout.Height(32));
                GUI.backgroundColor = background;
                if (pressed)
                    Encrypt();
            }
#endif
            EditorGUILayout.Space(SectionSpacing);
        }

        void Encrypt()
        {
            serializedObject.ApplyModifiedProperties();
            root.Encrypt(bUseSmallMipTexture.boolValue, false);
            GUIUtility.ExitGUI();
        }

        void DrawDebug()
        {
            debug = Foldout(debug, Lang("Debug"), EditorStyles.label);
            if (!debug)
                return;

            Hint(Lang("Detected: ") + string.Join(", ", DetectedTools()));
            if (GUILayout.Button(Lang("Create bug report")))
            {
                serializedObject.ApplyModifiedProperties();
                BugReport.Save(root, DetectedTools(), Lang("Save bug report"));
                GUIUtility.ExitGUI();
            }
            Hint(Lang("The report includes the avatar's key bytes. Send it only to the developer."));
            GUILayout.Space(10);

#if MODULAR
            using (new EditorGUI.DisabledScope(!CanEncrypt()))
            {
                if (GUILayout.Button(Lang("Manual Encrypt! (for testing)")))
                    Encrypt();
            }
#endif
            if (GUILayout.Button(Lang("Chacha8 test")))
                Test.ChachaTest(root.GetKeyBytes());
            GUILayout.Space(10);

            textureList.DoLayoutList();
            if (GUILayout.Button(Lang("Encrypt")))
            {
                Texture2D last = null;
                for (int i = 0; i < debugTextures.Count; i++)
                {
                    Texture2D texture = debugTextures[i];
                    if (texture == null)
                        continue;

                    TextureSettings.SetRWEnableTexture(texture);

                    // BC7 can only be encrypted with ChaCha.
                    IEncryptor cipher = texture.format == TextureFormat.BC7 ? new Chacha20() : new XXTEA();
                    var result = TextureEncryptManager.EncryptTexture(texture, root.GetKeyBytes(), cipher);
                    if (result.Texture1 == null)
                        continue;

                    last = result.Texture1;

                    var writer = new AssetWriter();
                    var outputPaths = new OutputPaths(root.AssetDir, root.Descriptor.gameObject);
                    outputPaths.PrepareFolders(writer, false);

                    writer.CreateAssetInFolder(result.Texture1, outputPaths.Folders.TexGuid, outputPaths.EncryptedTextureName(texture, 0));
                    if (result.Texture2 != null)
                    {
                        File.WriteAllBytes(writer.UniquePathInFolder(outputPaths.Folders.TexGuid, OutputPaths.Sanitize(texture.name) + "_encrypt.png"), result.Texture2.EncodeToPNG());
                        writer.CreateAssetInFolder(result.Texture2, outputPaths.Folders.TexGuid, outputPaths.EncryptedTextureName(texture, 2));
                    }
                    AssetDatabase.SaveAssets();

                    AssetDatabase.Refresh();
                }
                if (last != null)
                    Selection.activeObject = last;
            }
        }

        List<string> DetectedTools()
        {
            var detected = new List<string>(shaders);
#if MODULAR
            detected.Add("Modular Avatar");
#endif
            return detected;
        }

        static GUIContent[] ToContents(string[] texts)
        {
            var contents = new GUIContent[texts.Length];
            for (int i = 0; i < texts.Length; i++)
                contents[i] = new GUIContent(texts[i]);
            return contents;
        }

        static bool IsNewerVersion(string latest, string current)
        {
            return Version.TryParse(latest, out Version latestParsed) &&
                   Version.TryParse(current, out Version currentParsed) &&
                   latestParsed > currentParsed;
        }

        [MenuItem("GameObject/ShellProtector")]
        static void AddShellProtector()
        {
            GameObject gameobject = Selection.activeTransform != null ? Selection.activeTransform.gameObject : null;
            var av3 = gameobject != null ? gameobject.GetComponent<VRCAvatarDescriptor>() : null;
            if (av3 == null)
            {
                ErrorWindow.ShowWindow("Can't find avatar decriptor!", Color.white);
                return;
            }

            var obj = new GameObject();
            obj.name = "ShellProtector";
            obj.transform.parent = gameobject.transform;

            // Reset() finds the avatar and adds Body; this covers the case where it didn't run.
            var shellProtector = obj.AddComponent<ShellProtector>();
            shellProtector.Descriptor = av3;
            shellProtector.Init();
            Undo.RegisterCreatedObjectUndo(obj, "Add ShellProtector");

            Selection.activeObject = obj;
        }

        public class ErrorWindow : EditorWindow
        {
            string msg;
            Color color;

            public static void ShowWindow(string msg, Color color)
            {
                ErrorWindow window = GetWindow<ErrorWindow>("ShellProtector Console");
                window.minSize = new Vector2(400, 200);
                window.maxSize = new Vector2(400, 200);
                window.msg = msg;
                window.color = color;
                window.Focus();
            }

            private void OnGUI()
            {
                GUIStyle styles = new GUIStyle();
                //styles.fontStyle = FontStyle.Bold;
                styles.normal.textColor = color;
                GUILayout.Label(msg, styles);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close"))
                {
                    Close();
                }
            }
        }
    }
}
#endif
