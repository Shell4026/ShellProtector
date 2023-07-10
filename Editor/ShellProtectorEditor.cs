using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Text;
using System;
using System.IO;

namespace Shell.Protector
{
    [CustomEditor(typeof(ShellProtector))]
    [CanEditMultipleObjects]
    public class ShellProtectorEditor : Editor
    {
        ReorderableList material_list;
        ReorderableList texture_list;

        SerializedProperty rounds;
        SerializedProperty filter;
        SerializedProperty algorithm;

        bool debug = false;
        bool option = false;

        readonly string[] languages = new string[2];
        readonly string[] filters = new string[2];
        readonly string[] enc_funcs = new string[1];
        // Start is called before the first frame update

        List<string> shaders = new List<string>();

        readonly LanguageManager lang = LanguageManager.GetInstance();

        Texture2D tex;

        void OnEnable()
        {
            ShellProtector root = target as ShellProtector;
            MonoScript monoScript = MonoScript.FromMonoBehaviour(root);
            string script_path = AssetDatabase.GetAssetPath(monoScript);
            root.asset_dir = Path.GetDirectoryName(Path.GetDirectoryName(script_path));

            material_list = new ReorderableList(serializedObject, serializedObject.FindProperty("material_list"), true, true, true, true);
            material_list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, lang.GetLang(root.lang, "Material List"));
            material_list.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = material_list.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            texture_list = new ReorderableList(serializedObject, serializedObject.FindProperty("texture_list"), true, true, true, true);
            texture_list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, lang.GetLang(root.lang, "Texture List"));
            texture_list.drawElementCallback = (rect, index, is_active, is_focused) =>
            {
                SerializedProperty element = texture_list.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
            };

            rounds = serializedObject.FindProperty("rounds");
            filter = serializedObject.FindProperty("filter");
            algorithm = serializedObject.FindProperty("algorithm");

            filters[0] = "Point";
            filters[1] = "Bilinear";

            enc_funcs[0] = "XXTEA";

            languages[0] = "English";
            languages[1] = "한국어";

            shaders = ShaderManager.GetInstance().CheckShader();
        }

        public override void OnInspectorGUI()
        {
            ShellProtector root = target as ShellProtector;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Languages: ");
            GUILayout.FlexibleSpace();
            root.lang_idx = EditorGUILayout.Popup(root.lang_idx, languages, GUILayout.Width(100));
            switch(root.lang_idx)
            {
                case 0:
                    root.lang = "eng";
                    break;
                case 1:
                    root.lang = "kor";
                    break;
                default:
                    root.lang = "eng";
                    break;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(lang.GetLang(root.lang, "Decteced shaders:") + string.Join(", ", shaders), EditorStyles.boldLabel);
            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.Label(lang.GetLang(root.lang, "Password (max:12)"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(lang.GetLang(root.lang, "You don't need to memorize your password."), EditorStyles.wordWrappedLabel);
            GUILayout.EndHorizontal();

            root.pwd = GUILayout.PasswordField(root.pwd, '*', 12, GUILayout.Width(100));

            serializedObject.Update();
            material_list.DoLayoutList();

            option = EditorGUILayout.Foldout(option, lang.GetLang(root.lang, "Options"));
            if(option)
            {
                GUILayout.Label(lang.GetLang(root.lang, "Encrytion algorithm"), EditorStyles.boldLabel);
                algorithm.intValue = EditorGUILayout.Popup(algorithm.intValue, enc_funcs, GUILayout.Width(120));

                GUILayout.Label(lang.GetLang(root.lang, "Texture filter"), EditorStyles.boldLabel);
                filter.intValue = EditorGUILayout.Popup(filter.intValue, filters, GUILayout.Width(100));

                GUILayout.Space(30);
            }

            if (GUILayout.Button(lang.GetLang(root.lang, "Encrypt!")))
                root.Encrypt();


            debug = EditorGUILayout.Foldout(debug, lang.GetLang(root.lang, "Debug"));
            if(debug)
            {
                GUILayout.Space(10);
                if (GUILayout.Button(lang.GetLang(root.lang, "XXTEA test")))
                    root.Test2();
                /*tex = EditorGUILayout.ObjectField(tex, typeof(Texture2D)) as Texture2D;
                if (GUILayout.Button("Test"))
                {
                    var data = tex.GetRawTextureData();
                    Texture2D tmp = new Texture2D(tex.width, tex.height, TextureFormat.DXT5, false);
                    tmp.LoadRawTextureData(data);
                    tmp.filterMode = FilterMode.Point;
                    AssetDatabase.CreateAsset(tmp, root.asset_dir + "/test.asset");
                }*/
                GUILayout.Space(10);

                texture_list.DoLayoutList();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(lang.GetLang(root.lang, "Encrypt")))
                {
                    Texture2D last = null;
                    for (int i = 0; i < texture_list.count; i++)
                    {
                        SerializedProperty element = texture_list.serializedProperty.GetArrayElementAtIndex(i);
                        Texture2D texture = element.objectReferenceValue as Texture2D;

                        root.SetRWEnableTexture(texture);

                        Texture2D[] encrypted_texture = root.GetEncryptTexture().TextureEncryptXXTEA(texture, root.MakeKeyBytes(root.pwd));

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