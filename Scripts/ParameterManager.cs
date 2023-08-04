#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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

    public static VRCExpressionParameters AddKeyParameter(VRCExpressionParameters vrc_parameters, int key_length, bool optimize = false)
    {
        var parameters = vrc_parameters.parameters;

        int size = key_length;

        if (optimize == true)
        {
            if (key_length == 4)
                size = 4;
            else if (key_length == 8)
                size = 5;
            else
                size = 6;
        }

        VRCExpressionParameters.Parameter[] tmp = new VRCExpressionParameters.Parameter[parameters.Length + size];
        int idx;
        for(idx = 0; idx < parameters.Length; ++idx)
            tmp[idx] = CloneParameter(parameters[idx]);
        if (optimize == false)
        {
            for (int i = 0; i < size; ++i)
            {
                var para = new VRCExpressionParameters.Parameter
                {
                    name = "pkey" + i,
                    saved = true,
                    networkSynced = true,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0.0f
                };

                tmp[idx + i] = para;
            }
        }
        else
        {
            var pkey = new VRCExpressionParameters.Parameter
            {
                name = "pkey",
                saved = true,
                networkSynced = true,
                valueType = VRCExpressionParameters.ValueType.Float,
                defaultValue = 0.0f
            };
            tmp[idx++] = pkey;
            var plock = new VRCExpressionParameters.Parameter
            {
                name = "encrypt_lock",
                saved = true,
                networkSynced = true,
                valueType = VRCExpressionParameters.ValueType.Bool,
                defaultValue = 0.0f
            };
            tmp[idx++] = plock;
            for (int i = 0; i < size - 2; ++i)
            {
                var para = new VRCExpressionParameters.Parameter
                {
                    name = "encrypt_switch" + i,
                    saved = true,
                    networkSynced = true,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 0.0f
                };
                tmp[idx + i] = para;
            }
        }
        VRCExpressionParameters result = new VRCExpressionParameters();
        result.name = vrc_parameters.name + "_encrypted";
        result.parameters = tmp;
        return result;
    }
}
#endif