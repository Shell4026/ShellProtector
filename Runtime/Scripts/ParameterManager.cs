#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Shell.Protector
{
public static class ParameterManager
{
    // Every key parameter is named by UserKey.ObfuscateParameter, so its name says nothing about its role
    // and differs per avatar. The OSC app derives the same names.
    public static string GetSyncedKeyName(int index, bool bLegacy, UserKey key)
    {
        if (bLegacy)
            return key.ObfuscateParameter("pkey");
        return key.ObfuscateParameter("pkey" + index);
    }
    public static string GetKeyName(int index, UserKey key) => key.ObfuscateParameter("key" + index);
    public static string GetSavedKeyName(int index, UserKey key) => key.ObfuscateParameter("saved_key" + index);
    public static string GetSyncSwitchName(int index, UserKey key) => key.ObfuscateParameter("encrypt_switch" + index);
    public static string GetSyncLockName(UserKey key) => key.ObfuscateParameter("encrypt_lock");
    public static string GetIsLocalName() => "IsLocal";


    public static VRCExpressionParameters AddKeyParameter(VRCExpressionParameters vrcParameters, int keyLength, int syncSize, UserKey key)
    {
        bool bLegacy = syncSize == 1;
        var parameters = new List<VRCExpressionParameters.Parameter>();

        // Local only, so it costs no sync bits. It just exposes the salt to the OSC app.
        parameters.Add(new VRCExpressionParameters.Parameter
        {
            name = key.SaltParameterName,
            saved = false,
            networkSynced = false,
            valueType = VRCExpressionParameters.ValueType.Bool,
            defaultValue = 0.0f
        });

        parameters.Add(new VRCExpressionParameters.Parameter
        {
            name = GetSyncLockName(key),
            saved = true,
            networkSynced = true,
            valueType = VRCExpressionParameters.ValueType.Bool,
            defaultValue = 0.0f
        });

        for (var i = 0; i < syncSize; i++)
        {
            parameters.Add(new VRCExpressionParameters.Parameter
            {
                name = GetSyncedKeyName(i, bLegacy, key),
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
                name = GetSyncSwitchName(i, key),
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
                name = GetKeyName(i, key),
                saved = false,
                networkSynced = false,
                valueType = VRCExpressionParameters.ValueType.Float,
                defaultValue = 0.0f
            });

            if (!bLegacy)
            {
                parameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = GetSavedKeyName(i, key),
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
}
#endif
