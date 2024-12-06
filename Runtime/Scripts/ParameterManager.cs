#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Shell.Protector;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

public static class ParameterManager
{
    public static string GetPKeySyncParameterName(int index)
    {
        if (index == 0) return "pkey"; // For backward compatibility
        return "pkey_sync" + index;
    }
    public static string GetPKeyParameterName(int index) => "pkey" + index;
    public static string GetSyncSwitchParameterName(int index) => "encrypt_switch" + index;
    public static string GetSyncLockParameterName() => "encrypt_lock";


    public static VRCExpressionParameters AddKeyParameter(VRCExpressionParameters vrcParameters, int keyLength, int syncSize,  bool useMultiplexing)
    {
        var parameters = new List<VRCExpressionParameters.Parameter>();

        if (useMultiplexing == false)
        {
            for (var i = 0; i < keyLength; ++i)
            {
                parameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = GetPKeyParameterName(i),
                    saved = true,
                    networkSynced = true,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0.0f
                });
            }
        }
        else
        {
            parameters.Add(new VRCExpressionParameters.Parameter
            {
                name = GetSyncLockParameterName(),
                saved = true,
                networkSynced = true,
                valueType = VRCExpressionParameters.ValueType.Bool,
                defaultValue = 0.0f
            });

            for (var i = 0; i < syncSize; i++)
            {
                parameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = GetPKeySyncParameterName(i),
                    saved = true,
                    networkSynced = true,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0.0f
                });
            }

            for (var i = 0; i < ShellProtector.GetRequiredSwitchCount(keyLength, syncSize); ++i)
            {
                parameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = GetSyncSwitchParameterName(i),
                    saved = true,
                    networkSynced = true,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 0.0f
                });
            }

            for (var i = 0; i < keyLength; ++i)
            {
                parameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = GetPKeyParameterName(i),
                    saved = false,
                    networkSynced = false,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0.0f
                });
            }
        }

        var result = ScriptableObject.CreateInstance<VRCExpressionParameters>();
        result.name = vrcParameters.name + "_encrypted";
        result.parameters = vrcParameters.parameters.Concat(parameters).ToArray();;
        return result;
    }
}
#endif