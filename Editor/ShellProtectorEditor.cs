#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Text;
using System;
using System.IO;
using System.Text.RegularExpressions;
using VRC.SDK3.Avatars.Components;

namespace Shell.Protector
{
    [CustomEditor(typeof(ShellProtector))]
    [CanEditMultipleObjects]
    public class ShellProtectorEditor : Editor
    {
        ShellProtector root = null;
        readonly LanguageManager lang = LanguageManager.GetInstance();

        ReorderableList gameobjectList;
        ReorderableList materialList;
        ReorderableList textureList;
        ReorderableList obfuscationList;

        SerializedProperty rounds;
        SerializedProperty filter;
        SerializedProperty fallback;
        SerializedProperty algorithm;
        SerializedProperty keySize;
        SerializedProperty keySizeIdx;
        SerializedProperty syncSize;
        SerializedProperty deleteFolders;
        SerializedProperty bUseSmallMipTexture;
        SerializedProperty bPreserveMMD;
        SerializedProperty turnOnAllSafetyFallback;
        ShellProtectorEditorViewModel viewModel;
        bool debug = false;
        bool option = true;
        bool obfuscatorOption = true;
        bool forceProgress = false;
        bool fallbackOption = true;

        readonly string[] languages = new string[3];
        readonly string[] encryptFunctions = new string[2];
        readonly string[] keyLengthLabels = new string[5];

        List<string> shaders = new List<string>();

        bool showPassword = false;

        Texture2D tex;

        string githubVersion;

        private string Lang(string word)
        {
            if (root == null)
                return "";
            return lang.GetLang(root.Language, word);
        }

        void OnEnable()
        {
            root = target as ShellProtector;

            MonoScript monoScript = MonoScript.FromMonoBehaviour(root);
            string scriptPath = AssetDatabase.GetAssetPath(monoScript);

            root.AssetDir = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath));

            gameobjectList = new ReorderableList(serializedObject, serializedObject.FindProperty("_gameObjectList"), true, true, true, true);
            gameobjectList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, Lang("Object list"));
            gameobjectList.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = gameobjectList.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            materialList = new ReorderableList(serializedObject, serializedObject.FindProperty("_materialList"), true, true, true, true);
            materialList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, Lang("Material List"));
            materialList.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = materialList.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            textureList = new ReorderableList(serializedObject, serializedObject.FindProperty("textureList"), true, true, true, true);
            textureList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, Lang("Texture List"));
            textureList.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = textureList.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            obfuscationList = new ReorderableList(serializedObject, serializedObject.FindProperty("_obfuscationRenderers"), true, true, true, true);
            obfuscationList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, Lang("BlendShape obfuscation"));
            obfuscationList.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = obfuscationList.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            #region SerializedObject
            rounds = serializedObject.FindProperty("_rounds");
            filter = serializedObject.FindProperty("_filter");
            fallback = serializedObject.FindProperty("_fallback");
            algorithm = serializedObject.FindProperty("_algorithm");
            keySize = serializedObject.FindProperty("_keySize");
            keySizeIdx = serializedObject.FindProperty("_keySizeIndex");
            syncSize = serializedObject.FindProperty("_syncSize");
            deleteFolders = serializedObject.FindProperty("_deleteFolders");
            bUseSmallMipTexture = serializedObject.FindProperty("_useSmallMipTexture");
            bPreserveMMD = serializedObject.FindProperty("_preserveMmd");
            turnOnAllSafetyFallback = serializedObject.FindProperty("_turnOnAllSafetyFallback");
            #endregion
            viewModel = new ShellProtectorEditorViewModel(root, keySize, syncSize, gameobjectList, materialList);

            encryptFunctions[0] = "XXTEA";
            encryptFunctions[1] = "Chacha8";

            languages[0] = "English";
            languages[1] = "한국어";
            languages[2] = "日本語";

            keyLengthLabels[0] = Lang("0 (Minimal security)");
            keyLengthLabels[1] = Lang("4 (Low security)");
            keyLengthLabels[2] = Lang("8 (Middle security)");
            keyLengthLabels[3] = Lang("12 (Hight security)");
            keyLengthLabels[4] = Lang("16 (Unbreakable security)");

            VersionManager.GetInstance().Refresh();

            shaders = AssetManager.GetInstance().CheckShader();
            AssetManager.GetInstance().CheckModular();
        }

        public override void OnInspectorGUI()
        {
            root = target as ShellProtector;

            root.Descriptor = EditorGUILayout.ObjectField(root.Descriptor, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang("Current version: ") + VersionManager.GetInstance().GetVersion());
            GUILayout.FlexibleSpace();
            GUILayout.Label(Lang("Lastest version: ") + VersionManager.GetInstance().GetGithubVersion(), EditorStyles.boldLabel);
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang("Languages: "));
            GUILayout.FlexibleSpace();

            root.LanguageIndex = EditorGUILayout.Popup(root.LanguageIndex, languages, GUILayout.Width(100));

            keyLengthLabels[0] = Lang("0 (Minimal security)");
            keyLengthLabels[1] = Lang("4 (Low security)");
            keyLengthLabels[2] = Lang("8 (Middle security)");
            keyLengthLabels[3] = Lang("12 (Hight security)");
            keyLengthLabels[4] = Lang("16 (Unbreakable security)");

            switch (root.LanguageIndex)
            {
                case 0:
                    root.Language = "eng";
                    break;
                case 1:
                    root.Language = "kor";
                    break;
                case 2:
                    root.Language = "jp";
                    break;
                default:
                    root.Language = "eng";
                    break;
            }

            GUILayout.EndHorizontal();

            GUILayout.Label(Lang("Decteced shaders:") + string.Join(", ", shaders), EditorStyles.boldLabel);
#if MODULAR
            GUILayout.Label(Lang("ModularAvatar: true"), EditorStyles.boldLabel);
#else
            GUILayout.Label(Lang("ModularAvatar: false"), EditorStyles.boldLabel);
#endif
            GUILayout.Space(20);

            GUILayout.Label(Lang("Password"), EditorStyles.boldLabel);

            if (keySize.intValue < 16)
            {
                int length = 16 - keySize.intValue;
                GUILayout.BeginHorizontal();
                root.FixedPassword = GUILayout.TextField(root.FixedPassword, length, GUILayout.Width(100));
                if (GUILayout.Button(Lang("Generate")))
                    root.FixedPassword = KeyGenerator.GenerateRandomString(length);
                GUILayout.FlexibleSpace();
                GUILayout.Label(Lang("A password that you don't need to memorize. (max:") + length + ")", EditorStyles.wordWrappedLabel);
                GUILayout.EndHorizontal();
            }
            if (keySize.intValue > 0)
            {
                GUILayout.BeginHorizontal();
                if(!showPassword)
                    root.UserPassword = GUILayout.PasswordField(root.UserPassword, '*', keySize.intValue, GUILayout.Width(100));
                else
                    root.UserPassword = GUILayout.TextField(root.UserPassword, keySize.intValue, GUILayout.Width(100));
                if (GUILayout.Button(Lang("Show")))
                    showPassword = !showPassword;
                GUILayout.FlexibleSpace();
                GUILayout.Label(Lang("This password should be memorized. (max:") + keySize.intValue + ")", EditorStyles.wordWrappedLabel);
                GUILayout.EndHorizontal();
            }
            serializedObject.Update();
            viewModel.Refresh();

            GUIStyle redStyle = new GUIStyle(GUI.skin.label);
            redStyle.normal.textColor = Color.red;
            redStyle.wordWrap = true;

            if (!viewModel.HasParameterAsset)
                GUILayout.Label(Lang("Cannot find VRCExpressionParameters in your avatar!"), redStyle);
            else
            {
                GUILayout.Label(Lang("Free parameter:") + viewModel.FreeParameter, EditorStyles.wordWrappedLabel);
            }
            GUILayout.Label(Lang("Parameters to be used:") + viewModel.UsedParameter, EditorStyles.wordWrappedLabel);

            gameobjectList.DoLayoutList();
            materialList.DoLayoutList();
            GUILayout.Label(Lang("Encrypting too many objects can cause lag when loading avatars in-game."));
            if(GUILayout.Button(Lang("Material advanced settings")))
            {
                MaterialAdvancedSettings.ShowWindow(root);
            }

            #region Options
            option = EditorGUILayout.Foldout(option, Lang("Options"));
            if(option)
            {
                GUILayout.Label(Lang("Max password length"), EditorStyles.boldLabel);
                keySizeIdx.intValue = EditorGUILayout.Popup(keySizeIdx.intValue, keyLengthLabels, GUILayout.Width(150));
                GUILayout.Space(10);

                switch (keySizeIdx.intValue)
                {
                    case 0:
                        keySize.intValue = 0;
                        break;
                    case 1:
                        keySize.intValue = 4;
                        break;
                    case 2:
                        keySize.intValue = 8;
                        break;
                    case 3:
                        keySize.intValue = 12;
                        break;
                    case 4:
                        keySize.intValue = 16;
                        break;
                }

                var syncSize_value = syncSize.intValue;
                int syncSize_index = 0;
                //int[] syncSizeCandidates = { 1, 2, 4};
                //string[] selectableValues = { "1", "2", "4" };
                int[] syncSizeCandidates = { 1 };
                string[] selectableValues = { "1" };
                for (int i = 0; i < syncSizeCandidates.Length; i++)
                    if (syncSizeCandidates[i] == syncSize_value)
                        syncSize_index = i;

                if(keySize.intValue > 0)
                {
                    GUILayout.Label(Lang("Sync speed"), EditorStyles.boldLabel);
                    syncSize_index = EditorGUILayout.Popup(syncSize_index, selectableValues, GUILayout.Width(100));
                    syncSize.intValue = syncSizeCandidates[syncSize_index];
                    GUILayout.Label(Lang("Under development."), EditorStyles.boldLabel);
                    //GUILayout.Label(Lang("When the Sync speed is 2 or higher, OSC1.7 or higher must be used."), EditorStyles.boldLabel);
                    GUILayout.Space(10);
                }

                GUILayout.Label(Lang("Encrytion algorithm"), EditorStyles.boldLabel);
                algorithm.intValue = EditorGUILayout.Popup(algorithm.intValue, encryptFunctions, GUILayout.Width(120));

                if (algorithm.intValue == 0)
                {
                    GUILayout.Label(Lang("Rounds"), EditorStyles.boldLabel);
                    GUILayout.BeginHorizontal();
#if UNITY_2022
                    rounds.uintValue = (uint)Mathf.RoundToInt(GUILayout.HorizontalSlider(rounds.uintValue, 6, 32, GUILayout.Width(100)));
                    rounds.uintValue = (uint)EditorGUILayout.IntField("", (int)rounds.uintValue, GUILayout.Width(50));
                    rounds.uintValue = Math.Clamp(rounds.uintValue, 6, 32);
#else
                    rounds.intValue = Mathf.RoundToInt(GUILayout.HorizontalSlider(rounds.intValue, 6, 32, GUILayout.Width(100)));
                    rounds.intValue = EditorGUILayout.IntField("", (int)rounds.intValue, GUILayout.Width(50));
                    rounds.intValue = Mathf.Clamp(rounds.intValue, 6, 32);
#endif
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Label(Lang("Number of encryption iterations. Higher values provide better security, but at the expense of performance."), EditorStyles.wordWrappedLabel);
                }
                GUILayout.Space(10);

                GUILayout.Label(Lang("Default texture filter"), EditorStyles.boldLabel);
                filter.intValue = EditorGUILayout.Popup(filter.intValue, ShellProtector.FilterStrings, GUILayout.Width(100));
                GUILayout.Label(Lang("Setting it to 'Point' may result in aliasing, but performance is better."), EditorStyles.wordWrappedLabel);

                //GUILayout.Label(Lang("Initial animation speed"), EditorStyles.boldLabel);
                //GUILayout.BeginHorizontal();
                //animation_speed.floatValue = GUILayout.HorizontalSlider(animation_speed.floatValue, 2.0f, 5.0f, GUILayout.Width(100));
                //animation_speed.floatValue = EditorGUILayout.FloatField("", animation_speed.floatValue, GUILayout.Width(50));
                //animation_speed.floatValue = Math.Clamp(animation_speed.floatValue, 2.0f, 5.0f);
                //GUILayout.FlexibleSpace();
                //GUILayout.Label(Lang("Avatar first load animation speed"), EditorStyles.wordWrappedLabel);
                //GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.Label(Lang("Delete folders that already exists when at creation time"), EditorStyles.boldLabel);
                deleteFolders.boolValue = EditorGUILayout.Toggle(deleteFolders.boolValue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.Label(Lang("Small mip texture"), EditorStyles.boldLabel);
                bUseSmallMipTexture.boolValue = EditorGUILayout.Toggle(bUseSmallMipTexture.boolValue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Label(Lang("It uses a smaller mipTexture to reduce memory usage and improve performance. It may look slightly different from the original when viewed from the side."), EditorStyles.wordWrappedLabel);

                GUILayout.Space(10);
            }

            obfuscatorOption = EditorGUILayout.Foldout(obfuscatorOption, Lang("Obfustactor Options"));
            if(obfuscatorOption)
            {
                obfuscationList.DoLayoutList();

                GUILayout.BeginHorizontal();
                GUILayout.Label(Lang("Preserve MMD BlendShapes"), EditorStyles.boldLabel);
                bPreserveMMD.boolValue = EditorGUILayout.Toggle(bPreserveMMD.boolValue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
            }

            fallbackOption = EditorGUILayout.Foldout(fallbackOption, Lang("Fallback Options"));
            if (fallbackOption)
            {
                GUILayout.Label(Lang("Opponents with Safety option turned on will see degraded textures instead of noise."));

                GUILayout.BeginHorizontal();
                GUILayout.Label(Lang("Change all Safety Fallback settings of shader to Unlit."), EditorStyles.boldLabel);
                turnOnAllSafetyFallback.boolValue = EditorGUILayout.Toggle(turnOnAllSafetyFallback.boolValue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Label(Lang("Default fallback texture"), EditorStyles.boldLabel);
                fallback.intValue = EditorGUILayout.Popup(fallback.intValue, ShellProtector.FallbackStrings, GUILayout.Width(100));
            }
#endregion

            viewModel.Refresh();
            if (!viewModel.HasEnoughParameterSpace)
            {
                GUILayout.Label(Lang("Not enough parameter space!"), redStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label(Lang("Force progress"));
                forceProgress = EditorGUILayout.Toggle(forceProgress);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUI.enabled = forceProgress;
            }
            if (!viewModel.HasTargets)
                GUI.enabled = false;


#if MODULAR
            if (GUILayout.Button(Lang("Manual Encrypt! (for testing)")))
#else
            if (GUILayout.Button(Lang("Encrypt!")))
#endif
                root.Encrypt(bUseSmallMipTexture.boolValue, false);
            GUI.enabled = true;

#if MODULAR
            GUIStyle modularStyle = new GUIStyle(GUI.skin.label);
            modularStyle.normal.textColor = Color.green;
            modularStyle.wordWrap = true;
            GUILayout.Label(Lang("Modular avatars exist. It is automatically encrypted on upload."), modularStyle);
#endif

            if (GUILayout.Button(Lang("Delete previously encrypted files") + String.Format("({0})", root.GetEncryptedFoldersCount())))
            {
                root.CleanEncrypted();
            }

            debug = EditorGUILayout.Foldout(debug, Lang("Debug"));
            if(debug)
            {
                GUILayout.Space(10);
                if (GUILayout.Button(Lang("XXTEA test")))
                    Test.XXTEATest(root.FixedPassword, root.UserPassword, root.GetKeySize());
                if (GUILayout.Button(Lang("Chacha8 test")))
                    Test.ChachaTest(root.FixedPassword, root.UserPassword, root.GetKeySize());
                GUILayout.Space(10);

                textureList.DoLayoutList();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Lang("Encrypt")))
                {
                    Texture2D last = null;
                    for (int i = 0; i < textureList.count; i++)
                    {
                        SerializedProperty element = textureList.serializedProperty.GetArrayElementAtIndex(i);
                        Texture2D texture = element.objectReferenceValue as Texture2D;

                        TextureSettings.SetRWEnableTexture(texture);

                        var result = TextureEncryptManager.EncryptTexture(texture, KeyGenerator.MakeKeyBytes(root.FixedPassword, root.UserPassword, keySize.intValue), new XXTEA());

                        last = result.Texture1;

                        if (!AssetDatabase.IsValidFolder(root.AssetDir + '/' + root.Descriptor.gameObject.name))
                            AssetDatabase.CreateFolder(root.AssetDir, root.Descriptor.gameObject.name);
                        if (!AssetDatabase.IsValidFolder(root.AssetDir + '/' + root.Descriptor.gameObject.name + "/mat"))
                            AssetDatabase.CreateFolder(root.AssetDir + '/' + root.Descriptor.gameObject.name, "mat");

                        AssetDatabase.CreateAsset(result.Texture1, root.AssetDir + '/' + root.Descriptor.gameObject.name + '/' + texture.name + "_encrypt.asset");
                        File.WriteAllBytes(root.AssetDir + '/' + root.Descriptor.gameObject.name + '/' + texture.name + "_encrypt.png", result.Texture2.EncodeToPNG());
                        if (result.Texture2 != null)
                            AssetDatabase.CreateAsset(result.Texture2, root.AssetDir + '/' + root.Descriptor.gameObject.name + '/' + texture.name + "_encrypt2.asset");
                        AssetDatabase.SaveAssets();

                        AssetDatabase.Refresh();
                    }
                    if(last != null)
                        Selection.activeObject = last;
                }

                GUILayout.EndHorizontal();
            }
            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("GameObject/ShellProtector")]
        static void AddShellProtector()
        {
            LanguageManager lang = LanguageManager.GetInstance();

            GameObject gameobject = Selection.activeTransform.gameObject;
            var av3 = gameobject.GetComponent<VRCAvatarDescriptor>();
            if(av3 == null)
            {
                ErrorWindow.ShowWindow("Can't find avatar decriptor!", Color.white);
                return;
            }

            var obj = new GameObject();
            obj.name = "ShellProtector";
            obj.transform.parent = gameobject.transform;

            var shellProtector = obj.AddComponent<ShellProtector>();
            shellProtector.Descriptor = av3;
            shellProtector.Init();

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
