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
    //
    // The sync speed (syncSize) is the number of key bytes synced at once:
    // - 1: the OSC app multiplexes the key itself. It drives encrypt_lock, encrypt_switch* and the single
    //   synced key "pkey", so it has to keep running.
    // - 2 or 4: the OSC app writes each key byte to a saved local parameter (saved_key*). The avatar's mux
    //   layer then cycles through them with sync_lock, sync_switch* and the synced keys pkey0..pkey{syncSize-1},
    //   so the key survives restarts without the OSC app. The lock and switches have names of their own so
    //   that the OSC app, which sends both protocols, never fights the mux layer over them.
    public static bool IsOscMultiplexed(int syncSize) => syncSize == 1;

    public static string GetSyncedKeyName(int index, int syncSize, UserKey key)
    {
        if (IsOscMultiplexed(syncSize))
            return key.ObfuscateParameter("pkey");
        return key.ObfuscateParameter("pkey" + index);
    }
    public static string GetKeyName(int index, UserKey key) => key.ObfuscateParameter("key" + index);
    public static string GetSavedKeyName(int index, UserKey key) => key.ObfuscateParameter("saved_key" + index);
    public static string GetSyncSwitchName(int index, int syncSize, UserKey key) =>
        key.ObfuscateParameter((IsOscMultiplexed(syncSize) ? "encrypt_switch" : "sync_switch") + index);
    public static string GetSyncLockName(int syncSize, UserKey key) =>
        key.ObfuscateParameter(IsOscMultiplexed(syncSize) ? "encrypt_lock" : "sync_lock");
    public static string GetIsLocalName() => "IsLocal";


    public static VRCExpressionParameters AddKeyParameter(VRCExpressionParameters vrcParameters, int keyLength, int syncSize, UserKey key)
    {
        bool oscMultiplexed = IsOscMultiplexed(syncSize);
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
            name = GetSyncLockName(syncSize, key),
            saved = true,
            networkSynced = true,
            valueType = VRCExpressionParameters.ValueType.Bool,
            defaultValue = 0.0f
        });

        for (var i = 0; i < syncSize; i++)
        {
            parameters.Add(new VRCExpressionParameters.Parameter
            {
                name = GetSyncedKeyName(i, syncSize, key),
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
                name = GetSyncSwitchName(i, syncSize, key),
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

            if (!oscMultiplexed)
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
