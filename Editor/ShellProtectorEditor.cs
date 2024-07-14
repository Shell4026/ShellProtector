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

        SerializedProperty rounds;
        SerializedProperty filter;
        SerializedProperty algorithm;
        SerializedProperty key_size;
        SerializedProperty key_size_idx;
        SerializedProperty animation_speed;
        SerializedProperty delete_folders;
        SerializedProperty parameter_multiplexing;
        SerializedProperty bUseSmallMipTexture;
        SerializedProperty bObfuscate;
        SerializedProperty bPreserveMMD;
        bool debug = false;
        bool option = true;
        bool ObfuscatorOption = true;
        bool forceProgress = false;

        readonly string[] languages = new string[3];
        readonly string[] filters = new string[2];
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

            #region SerializedObject
            rounds = serializedObject.FindProperty("rounds");
            filter = serializedObject.FindProperty("filter");
            algorithm = serializedObject.FindProperty("algorithm");
            key_size = serializedObject.FindProperty("key_size");
            key_size_idx = serializedObject.FindProperty("key_size_idx");
            animation_speed = serializedObject.FindProperty("animation_speed");
            delete_folders = serializedObject.FindProperty("delete_folders"); 
            parameter_multiplexing = serializedObject.FindProperty("parameter_multiplexing");
            bUseSmallMipTexture = serializedObject.FindProperty("bUseSmallMipTexture");
            bObfuscate = serializedObject.FindProperty("bObfuscate");
            bPreserveMMD = serializedObject.FindProperty("bPreserveMMD");
            #endregion

            filters[0] = "Point";
            filters[1] = "Bilinear";

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
            int using_parameter = (key_size.intValue * 8);
            if(parameter_multiplexing.boolValue == true)
            {
                int keys = key_size.intValue;
                switch(keys)
                {
                    case 4:
                        using_parameter = 8 + 3;
                        break;
                    case 8:
                        using_parameter = 8 + 4;
                        break;
                    case 12:
                        using_parameter = 8 + 5;
                        break;
                    case 16:
                        using_parameter = 8 + 5;
                        break;
                }
            }
            GUILayout.Label(Lang("Parameters to be used:") + using_parameter, EditorStyles.wordWrappedLabel);

            serializedObject.Update();
            game_object_list.DoLayoutList();
            material_list.DoLayoutList();

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

                GUILayout.Label(Lang("Encrytion algorithm"), EditorStyles.boldLabel);
                algorithm.intValue = EditorGUILayout.Popup(algorithm.intValue, enc_funcs, GUILayout.Width(120));

                if (algorithm.intValue == 0)
                {
                    GUILayout.Label(Lang("Rounds"), EditorStyles.boldLabel);
                    GUILayout.BeginHorizontal();
                    rounds.uintValue = (uint)Mathf.RoundToInt(GUILayout.HorizontalSlider(rounds.uintValue, 6, 32, GUILayout.Width(100)));
                    rounds.uintValue = (uint)EditorGUILayout.IntField("", (int)rounds.uintValue, GUILayout.Width(50));
                    rounds.uintValue = Math.Clamp(rounds.uintValue, 6, 32);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Label(Lang("Number of encryption iterations. Higher values provide better security, but at the expense of performance."), EditorStyles.wordWrappedLabel);
                }
                GUILayout.Space(10);

                GUILayout.Label(Lang("Texture filter"), EditorStyles.boldLabel);
                filter.intValue = EditorGUILayout.Popup(filter.intValue, filters, GUILayout.Width(100));
                GUILayout.Label(Lang("Setting it to 'Point' may result in aliasing, but performance is better."), EditorStyles.wordWrappedLabel);

                GUILayout.Label(Lang("Initial animation speed"), EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                animation_speed.floatValue = GUILayout.HorizontalSlider(animation_speed.floatValue, 1.0f, 256.0f, GUILayout.Width(100));
                animation_speed.floatValue = EditorGUILayout.FloatField("", animation_speed.floatValue, GUILayout.Width(50));
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
                GUILayout.Label(Lang("parameter-multiplexing"), EditorStyles.boldLabel);
                parameter_multiplexing.boolValue = EditorGUILayout.Toggle(parameter_multiplexing.boolValue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Label(Lang("The OSC program must always be on, but it consumes fewer parameters."), EditorStyles.wordWrappedLabel);

                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.Label(Lang("Small mip texture"), EditorStyles.boldLabel);
                bUseSmallMipTexture.boolValue = EditorGUILayout.Toggle(bUseSmallMipTexture.boolValue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Label(Lang("It uses a smaller mipTexture to reduce memory usage and improve performance. It may look slightly different from the original when viewed from the side."), EditorStyles.wordWrappedLabel);

                GUILayout.Space(10);
            }

            ObfuscatorOption = EditorGUILayout.Foldout(option, Lang("Obfustactor Options"));
            if(ObfuscatorOption)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Lang("Obfuscate 'Body' BlendShapes"), EditorStyles.boldLabel);
                bObfuscate.boolValue = EditorGUILayout.Toggle(bObfuscate.boolValue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.Label(Lang("Preserve MMD BlendShapes"), EditorStyles.boldLabel);
                bPreserveMMD.boolValue = EditorGUILayout.Toggle(bPreserveMMD.boolValue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
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

            debug = EditorGUILayout.Foldout(debug, Lang("Debug"));
            if(debug)
            {
                GUILayout.Space(10);
                if (GUILayout.Button(Lang("XXTEA test")))
                    root.Test2();
                if (GUILayout.Button(Lang("Chacha8 test")))
                    root.Test3();
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

                        ShellProtector.SetRWEnableTexture(texture);

                        Texture2D[] encrypted_texture = root.GetEncryptTexture().TextureEncrypt(texture, KeyGenerator.MakeKeyBytes(root.pwd, root.pwd2, key_size.intValue), new XXTEA());

                        last = encrypted_texture[0];

                        if (!AssetDatabase.IsValidFolder(root.asset_dir + '/' + root.gameObject.name))
                            AssetDatabase.CreateFolder(root.asset_dir, root.gameObject.name);
                        if (!AssetDatabase.IsValidFolder(root.asset_dir + '/' + root.gameObject.name + "/mat"))
                            AssetDatabase.CreateFolder(root.asset_dir + '/' + root.gameObject.name, "mat");

                        AssetDatabase.CreateAsset(encrypted_texture[0], root.asset_dir + '/' + root.gameObject.name + '/' + texture.name + "_encrypt.asset");
                        File.WriteAllBytes(root.asset_dir + '/' + root.gameObject.name + '/' + texture.name + "_encrypt.png", encrypted_texture[1].EncodeToPNG());
                        if (encrypted_texture[1] != null)
                            AssetDatabase.CreateAsset(encrypted_texture[1], root.asset_dir + '/' + root.gameObject.name + '/' + texture.name + "_encrypt2.asset");
                        AssetDatabase.SaveAssets();

                        AssetDatabase.Refresh();
                    }
                    if(last != null)
                        Selection.activeObject = last;
                }

                /*if (GUILayout.Button("Decrypt"))
                {
                    Texture2D last = null;
                    for (int i = 0; i < texture_list.count; i++)
                    {
                        SerializedProperty textureProperty = texture_list.serializedProperty.GetArrayElementAtIndex(i);
                        Texture2D texture = textureProperty.objectReferenceValue as Texture2D;

                        root.SetRWEnableTexture(texture);

                        Texture2D tmp = root.GetEncryptTexture().TextureDecryptXXTEA(texture, root.MakeKeyBytes(root.pwd));

                        if (root.asset_dir[root.asset_dir.Length - 1] == '/')
                            root.asset_dir = root.asset_dir.Remove(root.asset_dir.Length - 1);

                        if (!AssetDatabase.IsValidFolder(root.asset_dir + '/' + root.gameObject.name))
                            AssetDatabase.CreateFolder(root.asset_dir, root.gameObject.name);
                        if (!AssetDatabase.IsValidFolder(root.asset_dir + '/' + root.gameObject.name + "/mat"))
                            AssetDatabase.CreateFolder(root.asset_dir + '/' + root.gameObject.name, "mat");

                        System.IO.File.WriteAllBytes(root.asset_dir + '/' + root.gameObject.name + '/' + texture.name + "_decrypt.png", tmp.EncodeToPNG());
                        last = (Texture2D)AssetDatabase.LoadAssetAtPath(root.asset_dir + '/' + root.gameObject.name + '/' + texture.name + "_decrypt.png", typeof(Texture2D));

                        AssetDatabase.Refresh();
                    }
                    if (last != null)
                        Selection.activeObject = last;
                }*/

                GUILayout.EndHorizontal();
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif