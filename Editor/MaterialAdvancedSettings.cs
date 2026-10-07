#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector
{
    // Per-material options, in the same style as the inspector: titles and spacing instead of boxes, descriptions in
    // tooltips and hints, and problems as help boxes.
    public class MaterialAdvancedSettings : EditorWindow
    {
        ShellProtector protector;
        readonly AssetManager assetManager = AssetManager.GetInstance();
        readonly LanguageManager lang = LanguageManager.GetInstance();

        Vector2 scroll = Vector2.zero;

        [NonSerialized] GUIStyle titleStyle;
        [NonSerialized] GUIStyle materialStyle;

        const float MaterialSpacing = 14;
        const float Indent = 22;

        private void OnEnable() => Undo.undoRedoPerformed += RestoreOptions;
        private void OnDisable() => Undo.undoRedoPerformed -= RestoreOptions;

        private void RestoreOptions()
        {
            if (protector == null) return;
            protector.SyncMatOption();
            Repaint();
        }

        public static void ShowWindow(ShellProtector protector)
        {
#if UNITY_2022
            Rect main = EditorGUIUtility.GetMainWindowPosition();
#else
            Rect main = new Rect(0, 0, 1024, 768);
#endif
            MaterialAdvancedSettings window = GetWindow<MaterialAdvancedSettings>();
            window.protector = protector;
            window.titleContent = new GUIContent(window.Lang("Material advanced settings"));
            Rect pos = window.position;
            pos.x = main.x + main.width / 2 - 300;
            pos.y = main.y + main.height / 2 - 250;
            pos.width = Mathf.Max(pos.width, 600);
            pos.height = Mathf.Max(pos.height, 500);
            window.minSize = new Vector2(520, 400);
            window.maxSize = new Vector2(main.width, main.height);
            window.position = pos;

            window.Focus();
            window.Init();
        }

        private string Lang(string word)
        {
            if (protector == null)
                return "";
            return lang.GetLang(protector.Language, word);
        }

        void InitStyles()
        {
            if (titleStyle != null)
                return;
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 19, fixedHeight = 0 };
            materialStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, fixedHeight = 0 };
        }

        static void Hint(string text)
        {
            GUILayout.Label(text, EditorStyles.wordWrappedMiniLabel);
        }

        private void OnGUI()
        {
            if (protector == null) return;
            InitStyles();

            EditorGUILayout.Space(6);
            GUILayout.Label(Lang("Material advanced settings"), titleStyle);
            Hint(Lang("Unchecked materials are not encrypted. Emission maps are encrypted only for the checked slots."));
            EditorGUILayout.Space(10);

            scroll = GUILayout.BeginScrollView(scroll);
            if (protector.materialOptions.Count == 0)
                EditorGUILayout.HelpBox(Lang("Add the objects or materials to encrypt."), MessageType.Info);
            foreach (var option in protector.materialOptions)
            {
                if (option.Key == null)
                    continue;
                DrawMaterial(option.Key, option.Value);
                EditorGUILayout.Space(MaterialSpacing);
            }
            GUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button(Lang("Reset")))
            {
                Undo.RecordObject(protector, "Reset material options");
                protector.ResetMaterialOptions();
                Init();
                SaveOptions();
            }
            EditorGUILayout.Space(4);
        }

        void DrawMaterial(Material material, ShellProtector.MatOption option)
        {
            Texture2D mainTex = material.mainTexture as Texture2D;
            bool supported = assetManager.IsSupportShader(material.shader);

            if (option.Filter == -1)
                option.Filter = protector.GetDefaultFilter();
            if (option.Fallback == -1)
                option.Fallback = protector.GetDefaultFallback();

            EditorGUILayout.BeginHorizontal();
            bool active = EditorGUILayout.ToggleLeft(new GUIContent(material.name, Lang("Encrypt this material")), option.Active, materialStyle);
            GUILayout.FlexibleSpace();
            if (supported && assetManager.IsPoiyomi(material.shader))
            {
                bool reused = EmissionEncryption.SupportsEmission(protector.IsEncryptedBefore(material.shader));
                GUILayout.Label(new GUIContent(Lang(reused ? "Encrypted shader" : "New shader"),
                    Lang(reused ? "A shader injected before is reused." : "The shader is injected and compiled on this build.")), EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            int filter = option.Filter, fallback = option.Fallback, mask = option.EmissionMask;
            using (new EditorGUI.DisabledScope(!active))
            {
                EditorGUI.indentLevel++;
                // For pinging only: the materials come from the encryption targets.
                EditorGUILayout.ObjectField(Lang("Material"), material, typeof(Material), false);
                EditorGUILayout.ObjectField(Lang("Main texture"), mainTex, typeof(Texture2D), false, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                filter = EditorGUILayout.Popup(new GUIContent(Lang("Texture filter"), Lang("Setting it to 'Point' may result in aliasing, but performance is better.")), filter, ToContents(ShellProtector.FilterStrings));
                fallback = EditorGUILayout.Popup(new GUIContent(Lang("Fallback texture"), Lang("Opponents with Safety option turned on will see degraded textures instead of noise.")), fallback, ToContents(ShellProtector.FallbackStrings));
                if (supported)
                    mask = DrawEmission(material, mask);
                EditorGUI.indentLevel--;
            }

            if (active != option.Active || filter != option.Filter || fallback != option.Fallback || mask != option.EmissionMask)
            {
                Undo.RecordObject(protector, "Change material options");
                option.Active = active;
                option.Filter = filter;
                option.Fallback = fallback;
                option.EmissionMask = mask;
                SaveOptions();
            }

            if (!active)
                return;
            MaterialIssues.Issue issue = MaterialIssues.Check(material);
            if (issue != MaterialIssues.Issue.None)
                EditorGUILayout.HelpBox(Lang(MaterialIssues.Message(issue)), MessageType.Error);
        }

        // One toggle per emission slot of the shader. A slot without a texture can't be selected.
        int DrawEmission(Material material, int mask)
        {
            bool poiyomi = assetManager.IsPoiyomi(material.shader);
            string[] maps = MaterialIssues.EmissionMaps(material);
            var unsupported = new List<string>();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(Lang("Encrypt emission"), Lang("Selected emission maps emit no light until the correct password is entered. Masks and gradients are not encrypted.")));
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            for (int slot = 0; slot < maps.Length; slot++)
            {
                if (!material.HasProperty(maps[slot]))
                    continue;
                Texture texture = material.GetTexture(maps[slot]);
                bool selected = (mask & (1 << slot)) != 0;
                string name = "Emission " + (poiyomi ? slot : slot + 1);
                string tooltip = texture != null ? texture.name + " (" + maps[slot] + ")" : Lang("No texture in this slot.");
                using (new EditorGUI.DisabledScope(texture == null && !selected))
                {
                    if (GUILayout.Toggle(selected, new GUIContent(name, tooltip), GUILayout.ExpandWidth(false)) != selected)
                        mask ^= 1 << slot;
                }
                GUILayout.Space(6);
                if (selected && texture != null && !MaterialIssues.IsSupportedEmission(texture))
                    unsupported.Add(name);
            }
            EditorGUI.indentLevel = indent;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (unsupported.Count > 0)
                EditorGUILayout.HelpBox(string.Join(", ", unsupported) + ": " + Lang("Emission maps must be power-of-two RGB24, RGBA32, DXT1 or DXT5 textures (DXT: at least 8x4)."), MessageType.Error);
            return mask;
        }

        static GUIContent[] ToContents(string[] texts)
        {
            var contents = new GUIContent[texts.Length];
            for (int i = 0; i < texts.Length; i++)
                contents[i] = new GUIContent(texts[i]);
            return contents;
        }

        private void OnLostFocus()
        {
            SaveOptions();
        }

        private void SaveOptions()
        {
            if (protector == null) return;
            protector.SaveMatOption();
            EditorUtility.SetDirty(protector);
            PrefabUtility.RecordPrefabInstancePropertyModifications(protector);
        }

        private void Init()
        {
            protector.SyncMatOption();
            var mats = protector.GetMaterials();
            HashSet<Material> matSets = new HashSet<Material>();
            foreach (var mat in mats)
            {
                matSets.Add(mat);
                if (!protector.materialOptions.ContainsKey(mat))
                {
                    var option = new ShellProtector.MatOption();
                    option.Active = true;
                    protector.materialOptions.Add(mat, option);
                }
            }

            List<Material> removed = new List<Material>();
            foreach (var pair in protector.materialOptions)
            {
                if (!matSets.Contains(pair.Key))
                {
                    removed.Add(pair.Key);
                }
            }
            foreach (var mat in removed)
                protector.materialOptions.Remove(mat);
        }
    }
}
#endif
