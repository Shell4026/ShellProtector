#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shell.Protector;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Shell.Protector.Tests
{
    internal static class VrcExpressionParametersTestUtil
    {
        public struct ParameterSpec
        {
            public string Name;
            public bool Saved;
            public bool NetworkSynced;
            public string ValueType;
            public float DefaultValue;
        }

        public struct ParameterSnapshot
        {
            public string Name;
            public bool Saved;
            public bool NetworkSynced;
            public string ValueType;
            public float DefaultValue;
        }

        private static Type expressionParametersType;
        private static Type parameterType;
        private static Type valueTypeEnum;

        public static ScriptableObject Create(string name, params ParameterSpec[] specs)
        {
            ScriptableObject parameters = ScriptableObject.CreateInstance(ExpressionParametersType);
            parameters.name = name;
            SetParameters(parameters, specs);
            return parameters;
        }

        public static ScriptableObject AddKeyParameter(ScriptableObject parameters, int keyLength, int syncSize)
        {
            MethodInfo method = typeof(ParameterManager).GetMethod(
                "AddKeyParameter",
                BindingFlags.Public | BindingFlags.Static);
            AssertMethod(method, "ParameterManager.AddKeyParameter");
            return (ScriptableObject)method.Invoke(null, new object[] { parameters, keyLength, syncSize });
        }

        public static IReadOnlyList<ParameterSnapshot> Read(ScriptableObject parameters)
        {
            object[] values = GetParameterArray(parameters);
            return values.Select(ReadParameter).ToArray();
        }

        public static void SetDescriptorParameters(VRCAvatarDescriptor descriptor, ScriptableObject parameters)
        {
            FieldInfo field = typeof(VRCAvatarDescriptor).GetField("expressionParameters");
            if (field != null)
            {
                field.SetValue(descriptor, parameters);
                return;
            }

            PropertyInfo property = typeof(VRCAvatarDescriptor).GetProperty("expressionParameters");
            if (property != null)
            {
                property.SetValue(descriptor, parameters);
                return;
            }

            SerializedObject serializedDescriptor = new SerializedObject(descriptor);
            SerializedProperty serializedProperty = serializedDescriptor.FindProperty("expressionParameters");
            if (serializedProperty == null)
                throw new MissingMemberException(typeof(VRCAvatarDescriptor).FullName, "expressionParameters");

            serializedProperty.objectReferenceValue = parameters;
            serializedDescriptor.ApplyModifiedPropertiesWithoutUndo();
        }

        public static ScriptableObject GetDescriptorParameters(VRCAvatarDescriptor descriptor)
        {
            FieldInfo field = typeof(VRCAvatarDescriptor).GetField("expressionParameters");
            if (field != null)
                return (ScriptableObject)field.GetValue(descriptor);

            PropertyInfo property = typeof(VRCAvatarDescriptor).GetProperty("expressionParameters");
            if (property != null)
                return (ScriptableObject)property.GetValue(descriptor);

            SerializedObject serializedDescriptor = new SerializedObject(descriptor);
            SerializedProperty serializedProperty = serializedDescriptor.FindProperty("expressionParameters");
            return serializedProperty == null ? null : serializedProperty.objectReferenceValue as ScriptableObject;
        }

        private static Type ExpressionParametersType
        {
            get
            {
                expressionParametersType ??= FindType("VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters");
                return expressionParametersType;
            }
        }

        private static Type ParameterType
        {
            get
            {
                parameterType ??= ExpressionParametersType.GetNestedType("Parameter");
                return parameterType;
            }
        }

        private static Type ValueTypeEnum
        {
            get
            {
                valueTypeEnum ??= ExpressionParametersType.GetNestedType("ValueType");
                return valueTypeEnum;
            }
        }

        private static void SetParameters(ScriptableObject parameters, ParameterSpec[] specs)
        {
            Array values = Array.CreateInstance(ParameterType, specs.Length);
            for (int i = 0; i < specs.Length; i++)
            {
                object parameter = Activator.CreateInstance(ParameterType);
                SetMember(parameter, "name", specs[i].Name);
                SetMember(parameter, "saved", specs[i].Saved);
                SetMember(parameter, "networkSynced", specs[i].NetworkSynced);
                SetMember(parameter, "valueType", Enum.Parse(ValueTypeEnum, specs[i].ValueType));
                SetMember(parameter, "defaultValue", specs[i].DefaultValue);
                values.SetValue(parameter, i);
            }

            SetMember(parameters, "parameters", values);
        }

        private static object[] GetParameterArray(ScriptableObject parameters)
        {
            object value = GetMember(parameters, "parameters");
            if (value is Array array)
            {
                object[] values = new object[array.Length];
                array.CopyTo(values, 0);
                return values;
            }

            return Array.Empty<object>();
        }

        private static ParameterSnapshot ReadParameter(object parameter)
        {
            return new ParameterSnapshot
            {
                Name = (string)GetMember(parameter, "name"),
                Saved = (bool)GetMember(parameter, "saved"),
                NetworkSynced = (bool)GetMember(parameter, "networkSynced"),
                ValueType = GetMember(parameter, "valueType").ToString(),
                DefaultValue = (float)GetMember(parameter, "defaultValue")
            };
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);

            if (type == null)
                throw new TypeLoadException(fullName);

            return type;
        }

        private static object GetMember(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name);
            if (field != null)
                return field.GetValue(target);

            PropertyInfo property = target.GetType().GetProperty(name);
            if (property != null)
                return property.GetValue(target);

            throw new MissingMemberException(target.GetType().FullName, name);
        }

        private static void SetMember(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = target.GetType().GetProperty(name);
            if (property != null)
            {
                property.SetValue(target, value);
                return;
            }

            throw new MissingMemberException(target.GetType().FullName, name);
        }

        private static void AssertMethod(MethodInfo method, string name)
        {
            if (method == null)
                throw new MissingMethodException(name);
        }
    }
}
#endif
