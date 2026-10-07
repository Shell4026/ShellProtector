#if UNITY_EDITOR
#if MODULAR
using nadena.dev.ndmf;
using Shell.Protector;
using UnityEngine;

[assembly: ExportsPlugin(typeof(NdmfPlugin))]

public class NdmfPlugin : Plugin<NdmfPlugin>
{
    protected override void Configure()
    {
        InPhase(BuildPhase.Transforming).
            AfterPlugin("nadena.dev.modular-avatar").
            Run("Encrypting2", ctx =>
            {
                Debug.Log("After encrypting Shell Protector");
                var shellProtector = ctx.AvatarRootObject.GetComponentInChildren<ShellProtector>(true);
                if (shellProtector)
                    shellProtector.Encrypt(isModular: true);
            });
    }
}
#endif
#endif
