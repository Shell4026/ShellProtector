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
        VRCExpressionParameters result = new VRCExpressionParameters();
        result.name = vrc_parameters.name + "_encrypted";

        var parameters = vrc_parameters.parameters;

        int switch_size = 0;
        int etc = 0;
        if (optimize == true)
        {
            etc = 2; //lock + pkey(sync)
            switch(key_length)
            {
                case 4:
                    switch_size = 2;
                    break;
                case 8:
                    switch_size = 3;
                    break;
                case 12:
                case 16:
                    switch_size = 4;
                    break;
                default:
                    Debug.LogErrorFormat("ParameterManager: key_length = {} is wrong!", key_length);
                    return result;
            }  
        }

        VRCExpressionParameters.Parameter[] tmp = new VRCExpressionParameters.Parameter[parameters.Length + switch_size + key_length + etc];
        int idx;
        for(idx = 0; idx < parameters.Length; ++idx)
            tmp[idx] = CloneParameter(parameters[idx]);

        if (optimize == false)
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
            var pkey = new VRCExpressionParameters.Parameter
            {
                name = "pkey",
                saved = true,
                networkSynced = true,
                valueType = VRCExpressionParameters.ValueType.Float,
                defaultValue = 0.0f
            };
            tmp[idx++] = pkey;
            for (int i = 0; i < key_length; ++i)
            {
                var para = new VRCExpressionParameters.Parameter
                {
                    name = "pkey" + i,
                    saved = true,
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
            for (int i = 0; i < switch_size; ++i)
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