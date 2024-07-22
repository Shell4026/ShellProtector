#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Shell.Protector.ShellProtector;

namespace Shell.Protector
{
    public class MaterialAdvancedSettings : EditorWindow
    {
        ShellProtector protector;
        AssetManager assetManager = AssetManager.GetInstance();
        readonly LanguageManager lang = LanguageManager.GetInstance();

        Vector2 scroll = Vector2.zero;

        public static void ShowWindow(ShellProtector protector)
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();

            MaterialAdvancedSettings window = GetWindow<MaterialAdvancedSettings>("Material advanced settings");
            Rect pos = window.position;
            pos.x = main.x + main.width / 2 - 200;
            pos.y = main.y + main.height / 2 - 200;
            window.minSize = new Vector2(400, 400);
            window.maxSize = new Vector2(main.width, main.height);
            window.position = pos;
            window.protector = protector;

            window.Focus();
            window.Init();
        }
        private string Lang(string word)
        {
            if (protector == null)
                return "";
            return lang.GetLang(protector.lang, word);
        }

        private void OnGUI()
        {
            GUIStyle redStyle = new GUIStyle(GUI.skin.label);
            redStyle.normal.textColor = Color.red;
            redStyle.wordWrap = true;

            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var option in protector.matOptions)
            {
                Texture2D mainTex = option.Key.mainTexture as Texture2D;

                GUILayout.BeginHorizontal();
                option.Value.active = GUILayout.Toggle(option.Value.active, "");
                EditorGUILayout.ObjectField(option.Key, typeof(Material), true);
                EditorGUILayout.ObjectField(mainTex, typeof(Texture2D), true);
                if (option.Value.filter == -1)
                    option.Value.filter = protector.GetDefaultFilter();
                option.Value.filter = EditorGUILayout.Popup(option.Value.filter, ShellProtector.filterStrings, GUILayout.Width(100));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (!assetManager.IsSupportShader(option.Key.shader))
                {
                    GUILayout.Label(Lang("Not supported shader"), redStyle);
                }
                else
                {
                    if (mainTex == null)
                    {
                        GUILayout.Label(Lang("The main texture is empty."), redStyle);
                    }
                    else if (
                        mainTex.format != TextureFormat.DXT1 &&
                        mainTex.format != TextureFormat.DXT5 &&
                        mainTex.format != TextureFormat.RGB24 &&
                        mainTex.format != TextureFormat.RGBA32)
                    {
                        GUILayout.Label(Lang("The main texture is not supported format."), redStyle);
                    }
                }
            }
            GUILayout.EndScrollView();
            if(GUILayout.Button(Lang("Reset")))
            {
                protector.matOptions.Clear();
                Init();
            }
        }

        private void Init()
        {
            var mats = protector.GetMaterials();
            HashSet<Material> matSets = new HashSet<Material>();
            foreach (var mat in mats)
            {
                matSets.Add(mat);
                if (!protector.matOptions.ContainsKey(mat))
                {
                    var option = new ShellProtector.MatOption();
                    option.active = true;
                    protector.matOptions.Add(mat, option);
                }
            }

            List<Material> removed = new List<Material>();
            foreach (var pair in protector.matOptions)
            {
                if (!matSets.Contains(pair.Key))
                {
                    removed.Add(pair.Key);
                }
            }
            foreach (var mat in removed)
                protector.matOptions.Remove(mat);
        }
    }
}
#endif