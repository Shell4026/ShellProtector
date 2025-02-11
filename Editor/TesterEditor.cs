﻿#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    [CustomEditor(typeof(ShellProtectorTester))]
    [CanEditMultipleObjects]
    public class TesterEditor : Editor
    {
        ShellProtectorTester root = null;
        readonly LanguageManager lang = LanguageManager.GetInstance();

        readonly string[] languages = new string[2];
        private string Lang(string word)
        {
            if (root == null)
                return "";
            return lang.GetLang(root.lang, word);
        }
        public void OnEnable()
        {
            root = (ShellProtectorTester)target;

            languages[0] = "English";
            languages[1] = "한국어";
        }
        public override void OnInspectorGUI()
        {
            root.protector = EditorGUILayout.ObjectField(root.protector, typeof(ShellProtector), GUILayout.Width(200)) as ShellProtector;

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang("Max password length"));
            GUILayout.FlexibleSpace();
            root.userKeyLength = EditorGUILayout.IntField(root.userKeyLength, GUILayout.Width(50));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang("Languages: "));
            GUILayout.FlexibleSpace();

            GUILayout.Space(10);

            root.lang_idx = EditorGUILayout.Popup(root.lang_idx, languages, GUILayout.Width(100));
            switch (root.lang_idx)
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
            
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Lang("Off fallback")))
            {
                root.ResetEncryption();
                root.protector.SetMaterialFallbackValue(root.transform.root.gameObject, false);
            }
            if (GUILayout.Button(Lang("On fallback")))
            {
                root.ResetEncryption();
                root.protector.SetMaterialFallbackValue(root.transform.root.gameObject, true);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (root.userKeyLength == 0)
            {
                GUILayout.Label(Lang("It's okay for the 0-digit password to be the same as the original."));
            }
            else
            {
                GUILayout.Label(Lang("If it looks like its original appearance when pressed, it's a success."));
                GUILayout.Label(Lang("It looks strange, try restarting Unity and checking back."));
                if (GUILayout.Button(Lang("Check encryption success")))
                    root.CheckEncryption();
            }
            GUILayout.Label(Lang("Press it before uploading."));
            if (GUILayout.Button(Lang("Done & Reset")))
            {
                root.ResetEncryption();
                //DestroyImmediate(root.GetComponent<ShellProtectorTester>());
            }
        }
    }
}
#endif