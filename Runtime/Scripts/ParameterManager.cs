#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Shell.Protector;
using UnityEngine;
using VRC.SDK3.Avatars;
using VRC.SDK3.Avatars.ScriptableObjects;

public static class ParameterManager
{
    public static VRCExpressionParameters.Parameter CloneParameter(VRCExpressionParameters.Parameter parameter)
    {
        var tmp = new VRCExpressionParameters.Parameter();
        tmp.saved = parameter.saved;
        tmp.name = parameter.name;
        tmp.networkSynced = parameter.networkSynced;
        tmp.valueType = parameter.valueType;
        tmp.defaultValue = parameter.defaultValue;
        return tmp;
    }

    public static string GetPKeySyncParameterName(int index)
    {
        if (index == 0) return "pkey"; // For backward compatibility
        return "pkey_sync" + index;
    }

    public static VRCExpressionParameters AddKeyParameter(VRCExpressionParameters vrc_parameters, int key_length, int sync_size,  bool use_multiplexing = false)
    {
        VRCExpressionParameters result = ScriptableObject.CreateInstance<VRCExpressionParameters>();
        result.name = vrc_parameters.name + "_encrypted";

        var parameters = vrc_parameters.parameters;

        int etc = 0;
        if (use_multiplexing)
        {
            etc = 1 + sync_size; // encrypt_lock + pkey_sync
        }

        VRCExpressionParameters.Parameter[] tmp = new VRCExpressionParameters.Parameter[parameters.Length + ShellProtector.GetRequiredSwitchCount(key_length, sync_size) + key_length + etc];
        int idx;
        for(idx = 0; idx < parameters.Length; ++idx)
            tmp[idx] = CloneParameter(parameters[idx]);

        if (use_multiplexing == false)
        {
            for (int i = 0; i < key_length; ++i)
            {
                var para = new VRCExpressionParameters.Parameter
                {
                    name = "pkey" + i,
                    saved = true,
                    networkSynced = true,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0.0f
                };

                tmp[idx++] = para;
            }
        }
        else
        {
            for (int i = 0; i < sync_size; i++)
            {
                var para = new VRCExpressionParameters.Parameter
                {
                    name = GetPKeySyncParameterName(i),
                    saved = true,
                    networkSynced = true,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0.0f
                };
                tmp[idx++] = para;
            }
            for (int i = 0; i < key_length; ++i)
            {
                var para = new VRCExpressionParameters.Parameter
                {
                    name = "pkey" + i,
                    saved = false,
                    networkSynced = false,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0.0f
                };

                tmp[idx++] = para;
            }
            var plock = new VRCExpressionParameters.Parameter
            {
                name = "encrypt_lock",
                saved = true,
                networkSynced = true,
                valueType = VRCExpressionParameters.ValueType.Bool,
                defaultValue = 0.0f
            };
            tmp[idx++] = plock;
            for (int i = 0; i < ShellProtector.GetRequiredSwitchCount(key_length, sync_size); ++i)
            {
                var para = new VRCExpressionParameters.Parameter
                {
                    name = "encrypt_switch" + i,
                    saved = true,
                    networkSynced = true,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 0.0f
                };
                tmp[idx++] = para;
            }
        }
        result.parameters = tmp;
        return result;
    }
}
#endif