#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Shell.Protector;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

public static class ParameterManager
{
    private const string Prefix = "SHELL_PROTECTOR_";
    public static string GetSyncedKeyName(int index, bool bLegacy = false)
    {
        if (bLegacy)
            return "pkey";
        return Prefix + "synced_key" + index;
    }
    public static string GetKeyName(int index) => Prefix + "key" + index;
    public static string GetSavedKeyName(int index) => Prefix + "saved_key" + index;
    public static string GetSyncSwitchName(int index, bool bLegacy = false)
    {
        if (bLegacy)
            return "encrypt_switch" + index;
        return Prefix + "sync_switch" + index; 
    }
    public static string GetSyncLockName(bool bLegacy = false)
    {
        if (bLegacy)
            return "encrypt_lock";
        return Prefix + "sync_lock"; 
    }
    public static string GetIsLocalName() => "IsLocal";


    public static VRCExpressionParameters AddKeyParameter(VRCExpressionParameters vrcParameters, int keyLength, int syncSize)
    {
        bool bLegacy = syncSize == 1;
        var parameters = new List<VRCExpressionParameters.Parameter>();

        parameters.Add(new VRCExpressionParameters.Parameter
        {
            name = GetSyncLockName(bLegacy),
            saved = true,
            networkSynced = true,
            valueType = VRCExpressionParameters.ValueType.Bool,
            defaultValue = 0.0f
        });

        for (var i = 0; i < syncSize; i++)
        {
            parameters.Add(new VRCExpressionParameters.Parameter
            {
                name = GetSyncedKeyName(i, bLegacy),
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
                name = GetSyncSwitchName(i, bLegacy),
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
                name = GetKeyName(i),
                saved = false,
                networkSynced = false,
                valueType = VRCExpressionParameters.ValueType.Float,
                defaultValue = 0.0f
            });

            if (!bLegacy)
            {
                parameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = GetSavedKeyName(i),
                    saved = true,
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