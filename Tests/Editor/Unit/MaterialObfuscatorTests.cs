#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Shell.Protector.Tests.Unit
{
    public class MaterialObfuscatorTests
    {
        // Shaped like a locked Poiyomi shader: a display name with braces and other property names that spans lines,
        // uniforms that are declared but baked in, render state from properties and a texture macro that pastes _ST.
        private const string LockedShader = @"Shader ""Hidden/Locked/ShellProtectorTests/Obfuscation""
{
    Properties
    {
        _MainTex (""Main"", 2D) = ""white"" {}
        _UnusedTex (""Unused { --{reference_properties:[_ReadValue]}"", 2D) = ""white"" {}
        _PastedTex (""Pasted"", 2D) = ""white"" {}
        _Color (""Color"", Color) = (1, 1, 1, 1)
        _Mode (""Mode--{on_value_actions:[
        {value:0,actions:[{type:SET_PROPERTY,data:_ZWrite=1}]}
        }]}"", Int) = 0
        _Inlined (""Inlined--{condition_showS:(_ReadValue==1)}"", Float) = 0.25
        _ReadValue (""Read"", Range(0, 1)) = 0.5
        _AnimatedValue (""Animated"", Float) = 0
        _ZWrite (""ZWrite"", Float) = 1
        _Count (""Count"", Integer) = 3
        _Tint (""Tint"", Vector) = (1, 2, 3, 4)
        _Key0 (""key0"", Float) = 0
    }
    SubShader
    {
        Pass
        {
            ZWrite [_ZWrite]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""
            #define DECLARE_ST(tex) float4 tex##_ST;
            #define SAMPLE_ST(tex, uv) tex.Sample(sampler##tex, uv * tex##_ST.xy)
            Texture2D _MainTex;
            Texture2D _PastedTex;
            SamplerState sampler_MainTex;
            SamplerState sampler_PastedTex;
            DECLARE_ST(_PastedTex)
            float _Inlined;
            half _ReadValue;
            float _AnimatedValue;
            float _Count;
            float4 _Tint;
            // _Inlined only appears in a comment
            /* and _Tint in a block comment */
            float4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            float4 frag() : SV_Target
            {
                float4 color = _MainTex.Sample(sampler_MainTex, float2(0, 0)) * 0.25;
                return color * _ReadValue * _AnimatedValue + SAMPLE_ST(_PastedTex, float2(0, 0));
            }
            ENDCG
        }
    }
}";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created)
                Object.DestroyImmediate(obj);
            created.Clear();
        }

        [Test]
        public void FindReadIdentifiers_SkipsPropertiesBlockCommentsAndDeclarations()
        {
            HashSet<string> read = MaterialObfuscator.FindReadIdentifiers(LockedShader, null);

            Assert.That(read, Does.Contain("_ReadValue"));
            Assert.That(read, Does.Contain("_MainTex"));
            Assert.That(read, Does.Contain("_ZWrite"), "Render state reads the property.");
            Assert.That(read, Does.Not.Contain("_Inlined"), "Only declared, in a comment and in a display name.");
            Assert.That(read, Does.Not.Contain("_Tint"), "Only declared and in a block comment.");
            Assert.That(read, Does.Not.Contain("_Count"));
            Assert.That(read, Does.Not.Contain("_UnusedTex"));
            Assert.That(read, Does.Not.Contain("_Mode"));
        }

        [Test]
        public void FindReadIdentifiers_AddsSuffixesThatMacrosPaste()
        {
            HashSet<string> read = MaterialObfuscator.FindReadIdentifiers(LockedShader, null);

            Assert.That(read, Does.Contain("_PastedTex_ST"));
        }

        [Test]
        public void FindReadIdentifiers_ScansIncludes()
        {
            const string source = "Shader \"X\" { SubShader { Pass { CGPROGRAM\n#include \"Inner.cginc\"\nENDCG } } }";
            HashSet<string> read = MaterialObfuscator.FindReadIdentifiers(source, include =>
                include == "Inner.cginc" ? "#include \"Nested.cginc\"\nfloat Get() { return _FromInclude; }" :
                include == "Nested.cginc" ? "float GetNested() { return _FromNested; }" : null);

            Assert.That(read, Does.Contain("_FromInclude"));
            Assert.That(read, Does.Contain("_FromNested"));
        }

        [Test]
        public void RemovePropertiesBlock_KeepsTheRestWhenTheBlockNeverCloses()
        {
            const string source = "Shader \"X\"\n{\n    Properties\n    {\n        _A (\"A\", Float) = 0\n";

            Assert.That(MaterialObfuscator.RemovePropertiesBlock(source), Is.EqualTo(source));
        }

        [Test]
        public void Apply_ResetsBakedValuesAndKeepsWhatTheShaderReads()
        {
            // The locked shader before injection lacks the encrypted copy's own property.
            Shader original = CreateShader(LockedShader.Replace("_Key0 (\"key0\", Float) = 0", ""));
            Shader encrypted = CreateShader(LockedShader.Replace("Obfuscation", "Obfuscation_encrypted"));
            Shader stale = CreateShader("Shader \"Hidden/ShellProtectorTests/Stale\"\n{\n    Properties\n    {\n        _Stale (\"Stale\", Float) = 0\n        _StaleTex (\"Stale\", 2D) = \"white\" {}\n    }\n    SubShader { Pass { } }\n}");
            Assert.That(original.FindPropertyIndex("_Key0"), Is.EqualTo(-1));
            Assert.That(encrypted.FindPropertyIndex("_Key0"), Is.GreaterThanOrEqualTo(0));

            Texture2D texture = Track(new Texture2D(4, 4));
            Material source = Track(new Material(original));
            source.SetOverrideTag("OriginalShader", ".poiyomi/Poiyomi Toon");
            source.SetOverrideTag("OriginalKeywords", "POI_MATCAP0 _EMISSION");
            source.SetOverrideTag("_AnimatedValueAnimated", "1");
            source.SetOverrideTag("_MainTex_texPack_a_channel", "4");

            Material material = Track(new Material(stale));
            material.SetFloat("_Stale", 7f);
            material.SetTexture("_StaleTex", texture);
            material.shader = encrypted;
            material.SetOverrideTag("OriginalShader", ".poiyomi/Poiyomi Toon");
            material.SetOverrideTag("OriginalKeywords", "POI_MATCAP0 _EMISSION");
            material.SetOverrideTag("_AnimatedValueAnimated", "1");
            material.SetOverrideTag("_MainTex_texPack_a_channel", "4");
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", new Vector2(2f, 2f));
            material.SetTexture("_UnusedTex", texture);
            material.SetTextureScale("_UnusedTex", new Vector2(3f, 3f));
            material.SetTexture("_PastedTex", texture);
            material.SetTextureScale("_PastedTex", new Vector2(4f, 4f));
            material.SetColor("_Color", new Color(0.5f, 0.25f, 0.125f, 1f));
            material.SetFloat("_Mode", 3f);
            material.SetFloat("_Inlined", 0.75f);
            material.SetFloat("_ReadValue", 0.9f);
            material.SetFloat("_AnimatedValue", 5f);
            material.SetFloat("_ZWrite", 0f);
            material.SetInteger("_Count", 9);
            material.SetVector("_Tint", new Vector4(9f, 9f, 9f, 9f));
            material.SetFloat("_Key0", 42f);

            int reset = MaterialObfuscator.Apply(material, source, MaterialObfuscator.FindReadIdentifiers(LockedShader, null));

            Assert.That(reset, Is.GreaterThan(0));
            // Baked values go back to the shader defaults.
            Assert.That(material.GetFloat("_Inlined"), Is.EqualTo(0.25f));
            Assert.That(material.GetFloat("_Mode"), Is.EqualTo(0f));
            Assert.That(material.GetInteger("_Count"), Is.EqualTo(3));
            Assert.That(material.GetVector("_Tint"), Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
            Assert.That(material.GetTexture("_UnusedTex"), Is.Null);
            Assert.That(material.GetTextureScale("_UnusedTex"), Is.EqualTo(Vector2.one));
            // What the shader reads keeps its value.
            Assert.That(material.GetFloat("_ReadValue"), Is.EqualTo(0.9f));
            Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(0f));
            Assert.That(material.GetFloat("_AnimatedValue"), Is.EqualTo(5f), "Thry marks it animated.");
            Assert.That(material.GetFloat("_Key0"), Is.EqualTo(42f), "Only the encrypted copy has it.");
            Assert.That(material.GetColor("_Color"), Is.EqualTo(new Color(0.5f, 0.25f, 0.125f, 1f)), "Fallback shaders read it.");
            Assert.That(material.GetTexture("_MainTex"), Is.EqualTo(texture));
            Assert.That(material.GetTextureScale("_MainTex"), Is.EqualTo(new Vector2(2f, 2f)));
            Assert.That(material.GetTexture("_PastedTex"), Is.EqualTo(texture));
            Assert.That(material.GetTextureScale("_PastedTex"), Is.EqualTo(new Vector2(4f, 4f)), "A macro pastes _ST onto the name.");
            // Thry's editor tags go, the lock state stays.
            Assert.That(material.GetTag("OriginalShader", false, ""), Is.EqualTo(".poiyomi/Poiyomi Toon"));
            Assert.That(material.GetTag("_AnimatedValueAnimated", false, ""), Is.EqualTo("1"));
            Assert.That(material.GetTag("OriginalKeywords", false, ""), Is.Empty);
            Assert.That(material.GetTag("_MainTex_texPack_a_channel", false, ""), Is.Empty);
            // Values of properties the shader doesn't have are gone.
            Assert.That(SavedNames(material, "m_Floats"), Does.Not.Contain("_Stale"));
            Assert.That(SavedNames(material, "m_TexEnvs"), Does.Not.Contain("_StaleTex"));
        }

        private Shader CreateShader(string source)
        {
            return Track(ShaderUtil.CreateShaderAsset(source, false));
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private static List<string> SavedNames(Material material, string map)
        {
            var names = new List<string>();
            SerializedProperty entries = new SerializedObject(material).FindProperty("m_SavedProperties").FindPropertyRelative(map);
            for (int i = 0; i < entries.arraySize; i++)
                names.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue);
            return names;
        }
    }
}
#endif
