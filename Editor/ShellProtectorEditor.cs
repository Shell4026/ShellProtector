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

        ReorderableList game_object_list;
        ReorderableList material_list;
        ReorderableList texture_list;
        ReorderableList obfuscationList;

        SerializedProperty rounds;
        SerializedProperty filter;
        SerializedProperty fallback;
        SerializedProperty algorithm;
        SerializedProperty key_size;
        SerializedProperty key_size_idx;
        SerializedProperty sync_size;
        SerializedProperty animation_speed;
        SerializedProperty delete_folders;
        SerializedProperty bUseSmallMipTexture;
        SerializedProperty bPreserveMMD;
        SerializedProperty turnOnAllSafetyFallback;
        bool debug = false;
        bool option = true;
        bool ObfuscatorOption = true;
        bool forceProgress = false;
        bool fallbackOption = true;

        readonly string[] languages = new string[3];
        readonly string[] enc_funcs = new string[2];
        readonly string[] key_lengths = new string[5];

        List<string> shaders = new List<string>();

        bool show_pwd = false;

        Texture2D tex;

        string github_version;

        private string Lang(string word)
        {
            if (root == null)
                return "";
            return lang.GetLang(root.lang, word);
        }

        void OnEnable()
        {
            root = target as ShellProtector;

            MonoScript monoScript = MonoScript.FromMonoBehaviour(root);
            string script_path = AssetDatabase.GetAssetPath(monoScript);

            root.asset_dir = Path.GetDirectoryName(Path.GetDirectoryName(script_path));

            game_object_list = new ReorderableList(serializedObject, serializedObject.FindProperty("gameobject_list"), true, true, true, true);
            game_object_list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, Lang("Object list"));
            game_object_list.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = game_object_list.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            material_list = new ReorderableList(serializedObject, serializedObject.FindProperty("material_list"), true, true, true, true);
            material_list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, Lang("Material List"));
            material_list.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = material_list.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            texture_list = new ReorderableList(serializedObject, serializedObject.FindProperty("texture_list"), true, true, true, true);
            texture_list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, Lang("Texture List"));
            texture_list.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = texture_list.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            obfuscationList = new ReorderableList(serializedObject, serializedObject.FindProperty("obfuscationRenderers"), true, true, true, true);
            obfuscationList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, Lang("BlendShape obfuscation"));
            obfuscationList.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = obfuscationList.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            #region SerializedObject
            rounds = serializedObject.FindProperty("rounds");
            filter = serializedObject.FindProperty("filter");
            fallback = serializedObject.FindProperty("fallback");
            algorithm = serializedObject.FindProperty("algorithm");
            key_size = serializedObject.FindProperty("key_size");
            key_size_idx = serializedObject.FindProperty("key_size_idx");
            sync_size = serializedObject.FindProperty("sync_size");
            animation_speed = serializedObject.FindProperty("animation_speed");
            delete_folders = serializedObject.FindProperty("delete_folders");
            bUseSmallMipTexture = serializedObject.FindProperty("bUseSmallMipTexture");
            bPreserveMMD = serializedObject.FindProperty("bPreserveMMD");
            turnOnAllSafetyFallback = serializedObject.FindProperty("turnOnAllSafetyFallback");
            #endregion

            enc_funcs[0] = "XXTEA";
            enc_funcs[1] = "Chacha8";

            languages[0] = "English";
            languages[1] = "한국어";
            languages[2] = "日本語";

            key_lengths[0] = Lang("0 (Minimal security)");
            key_lengths[1] = Lang("4 (Low security)");
            key_lengths[2] = Lang("8 (Middle security)");
            key_lengths[3] = Lang("12 (Hight security)");
            key_lengths[4] = Lang("16 (Unbreakable security)");

            VersionManager.GetInstance().Refresh();

            shaders = AssetManager.GetInstance().CheckShader();
            AssetManager.GetInstance().CheckModular();
        }

        public override void OnInspectorGUI()
        {
            root = target as ShellProtector;

            root.descriptor = EditorGUILayout.ObjectField(root.descriptor, typeof(VRCAvatarDescriptor)) as VRCAvatarDescriptor;
            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang("Current version: ") + VersionManager.GetInstance().GetVersion());
            GUILayout.FlexibleSpace();
            GUILayout.Label(Lang("Lastest version: ") + VersionManager.GetInstance().GetGithubVersion(), EditorStyles.boldLabel);
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang("Languages: "));
            GUILayout.FlexibleSpace();

            root.lang_idx = EditorGUILayout.Popup(root.lang_idx, languages, GUILayout.Width(100));

            key_lengths[0] = Lang("0 (Minimal security)");
            key_lengths[1] = Lang("4 (Low security)");
            key_lengths[2] = Lang("8 (Middle security)");
            key_lengths[3] = Lang("12 (Hight security)");
            key_lengths[4] = Lang("16 (Unbreakable security)");

            switch (root.lang_idx)
            {
                case 0:
                    root.lang = "eng";
                    break;
                case 1:
                    root.lang = "kor";
                    break;
                case 2:
                    root.lang = "jp";
                    break;
                default:
                    root.lang = "eng";
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

            if (key_size.intValue < 16)
            {
                int length = 16 - key_size.intValue;
                GUILayout.BeginHorizontal();
                root.pwd = GUILayout.TextField(root.pwd, length, GUILayout.Width(100));
                if (GUILayout.Button(Lang("Generate")))
                    root.pwd = KeyGenerator.GenerateRandomString(length);
                GUILayout.FlexibleSpace();
                GUILayout.Label(Lang("A password that you don't need to memorize. (max:") + length + ")", EditorStyles.wordWrappedLabel);
                GUILayout.EndHorizontal();
            }
            if (key_size.intValue > 0)
            {
                GUILayout.BeginHorizontal();
                if(!show_pwd)
                    root.pwd2 = GUILayout.PasswordField(root.pwd2, '*', key_size.intValue, GUILayout.Width(100));
                else
                    root.pwd2 = GUILayout.TextField(root.pwd2, key_size.intValue, GUILayout.Width(100));
                if (GUILayout.Button(Lang("Show")))
                    show_pwd = !show_pwd;
                GUILayout.FlexibleSpace();
                GUILayout.Label(Lang("This password should be memorized. (max:") + key_size.intValue + ")", EditorStyles.wordWrappedLabel);
                GUILayout.EndHorizontal();
            }
            int free_parameter = -1;

            GUIStyle red_style = new GUIStyle(GUI.skin.label);
            red_style.normal.textColor = Color.red;
            red_style.wordWrap = true;

            var parameters = root.GetParameter();
            if (parameters == null)
                GUILayout.Label(Lang("Cannot find VRCExpressionParameters in your avatar!"), red_style);
            else
            {
                free_parameter = 256 - parameters.CalcTotalCost();
                GUILayout.Label(Lang("Free parameter:") + free_parameter, EditorStyles.wordWrappedLabel);
            }
            int lock_size = 1;
            int switch_count = ShellProtector.GetRequiredSwitchCount(key_size.intValue, sync_size.intValue);
            int using_parameter = switch_count + lock_size + sync_size.intValue * 8;
            GUILayout.Label(Lang("Parameters to be used:") + using_parameter, EditorStyles.wordWrappedLabel);

            serializedObject.Update();
            game_object_list.DoLayoutList();
            material_list.DoLayoutList();
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
                key_size_idx.intValue = EditorGUILayout.Popup(key_size_idx.intValue, key_lengths, GUILayout.Width(150));

                switch (key_size_idx.intValue)
                {
                    case 0:
                        key_size.intValue = 0;
                        break;
                    case 1:
                        key_size.intValue = 4;
                        break;
                    case 2:
                        key_size.intValue = 8;
                        break;
                    case 3:
                        key_size.intValue = 12;
                        break;
                    case 4:
                        key_size.intValue = 16;
                        break;
                }

                var sync_size_value = sync_size.intValue;
                int sync_size_index = 0;
                int[] sync_size_caldidate = { 1, 2, 4};
                string[] selectable_values = { "1", "2", "4" };
                for (int i = 0; i < sync_size_caldidate.Length; i++)
                    if (sync_size_caldidate[i] == sync_size_value)
                        sync_size_index = i;

                if(key_size.intValue > 0)
                {
                    GUILayout.Label(Lang("Sync speed"), EditorStyles.boldLabel);
                    sync_size_index = EditorGUILayout.Popup(sync_size_index, selectable_values, GUILayout.Width(100));
                    sync_size.intValue = sync_size_caldidate[sync_size_index];
                }

                GUILayout.Label(Lang("Encrytion algorithm"), EditorStyles.boldLabel);
                algorithm.intValue = EditorGUILayout.Popup(algorithm.intValue, enc_funcs, GUILayout.Width(120));

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
                filter.intValue = EditorGUILayout.Popup(filter.intValue, ShellProtector.filterStrings, GUILayout.Width(100));
                GUILayout.Label(Lang("Setting it to 'Point' may result in aliasing, but performance is better."), EditorStyles.wordWrappedLabel);

                GUILayout.Label(Lang("Initial animation speed"), EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                animation_speed.floatValue = GUILayout.HorizontalSlider(animation_speed.floatValue, 2.0f, 5.0f, GUILayout.Width(100));
                animation_speed.floatValue = EditorGUILayout.FloatField("", animation_speed.floatValue, GUILayout.Width(50));
                animation_speed.floatValue = Math.Clamp(animation_speed.floatValue, 2.0f, 5.0f);
                GUILayout.FlexibleSpace();
                GUILayout.Label(Lang("Avatar first load animation speed"), EditorStyles.wordWrappedLabel);
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.Label(Lang("Delete folders that already exists when at creation time"), EditorStyles.boldLabel);
                delete_folders.boolValue = EditorGUILayout.Toggle(delete_folders.boolValue);
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

            ObfuscatorOption = EditorGUILayout.Foldout(ObfuscatorOption, Lang("Obfustactor Options"));
            if(ObfuscatorOption)
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
                fallback.intValue = EditorGUILayout.Popup(fallback.intValue, ShellProtector.fallbackStrings, GUILayout.Width(100));
            }
#endregion

            if (free_parameter - using_parameter < 0)
            {
                GUILayout.Label(Lang("Not enough parameter space!"), red_style);
                GUILayout.BeginHorizontal();
                GUILayout.Label(Lang("Force progress"));
                forceProgress = EditorGUILayout.Toggle(forceProgress);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUI.enabled = forceProgress;
            }
            if (game_object_list.count == 0 && material_list.count == 0)
                GUI.enabled = false;


#if MODULAR
            if (GUILayout.Button(Lang("Manual Encrypt!")))
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

            if (GUILayout.Button(Lang("Delete previously encrypted files") + String.Format("({0})", root.GetEncyryptedFoldersCount())))
            {
                root.DeleteEncyprtedFolders();
            }

            debug = EditorGUILayout.Foldout(debug, Lang("Debug"));
            if(debug)
            {
                GUILayout.Space(10);
                if (GUILayout.Button(Lang("XXTEA test")))
                    Test.XXTEATest(root.pwd, root.pwd2, root.GetKeySize());
                if (GUILayout.Button(Lang("Chacha8 test")))
                    Test.ChachaTest(root.pwd, root.pwd2, root.GetKeySize());
                GUILayout.Space(10);

                texture_list.DoLayoutList();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Lang("Encrypt")))
                {
                    Texture2D last = null;
                    for (int i = 0; i < texture_list.count; i++)
                    {
                        SerializedProperty element = texture_list.serializedProperty.GetArrayElementAtIndex(i);
                        Texture2D texture = element.objectReferenceValue as Texture2D;

                        TextureSettings.SetRWEnableTexture(texture);

                        var result = TextureEncryptManager.EncryptTexture(texture, KeyGenerator.MakeKeyBytes(root.pwd, root.pwd2, key_size.intValue), new XXTEA());

                        last = result.Texture1;

                        if (!AssetDatabase.IsValidFolder(root.asset_dir + '/' + root.descriptor.gameObject.name))
                            AssetDatabase.CreateFolder(root.asset_dir, root.descriptor.gameObject.name);
                        if (!AssetDatabase.IsValidFolder(root.asset_dir + '/' + root.descriptor.gameObject.name + "/mat"))
                            AssetDatabase.CreateFolder(root.asset_dir + '/' + root.descriptor.gameObject.name, "mat");

                        AssetDatabase.CreateAsset(result.Texture1, root.asset_dir + '/' + root.descriptor.gameObject.name + '/' + texture.name + "_encrypt.asset");
                        File.WriteAllBytes(root.asset_dir + '/' + root.descriptor.gameObject.name + '/' + texture.name + "_encrypt.png", result.Texture2.EncodeToPNG());
                        if (result.Texture2 != null)
                            AssetDatabase.CreateAsset(result.Texture2, root.asset_dir + '/' + root.descriptor.gameObject.name + '/' + texture.name + "_encrypt2.asset");
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
            shellProtector.descriptor = av3;
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