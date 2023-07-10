#if UNITY_EDITOR
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
            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang("Languages: "));
            GUILayout.FlexibleSpace();
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

            GUILayout.Label(Lang("Password"));
            root.pwd = GUILayout.PasswordField(root.pwd, '*', 4, GUILayout.Width(100));
            
            GUILayout.Space(10);

            GUILayout.Label(Lang("If it looks like its original appearance when pressed, it's a success."));
            if (GUILayout.Button(Lang("Check encryption success")))
                root.CheckEncryption();
            GUILayout.Label(Lang("Press it before uploading."));
            if (GUILayout.Button(Lang("Done")))
            {
                //root.ResetEncryption();
                DestroyImmediate(root.GetComponent<ShellProtectorTester>());
            }
        }
    }
}
#endif