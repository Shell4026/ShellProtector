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

    public static VRCExpressionParameters AddKeyParameter(VRCExpressionParameters vrc_parameters, int key_length)
    {
        var parameters = vrc_parameters.parameters;
        
        VRCExpressionParameters.Parameter[] tmp = new VRCExpressionParameters.Parameter[parameters.Length + key_length];
        int idx;
        for(idx = 0; idx < parameters.Length; ++idx)
            tmp[idx] = CloneParameter(parameters[idx]);
        for(int i = 0; i < key_length; ++i)
        {
            var para = new VRCExpressionParameters.Parameter();
            para.name = "key" + i;
            para.saved = true;
            para.networkSynced = true;
            para.valueType = VRCExpressionParameters.ValueType.Float;
            para.defaultValue = 0.0f;

            tmp[idx + i] = para;
        }

        VRCExpressionParameters result = new VRCExpressionParameters();
        result.name = vrc_parameters.name + "_encrypted";
        result.parameters = tmp;
        return result;
    }
}
#endif