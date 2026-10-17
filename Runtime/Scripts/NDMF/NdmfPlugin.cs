#if UNITY_EDITOR
#if MODULAR
using nadena.dev.ndmf;
using Shell.Protector;
using UnityEngine;

[assembly: ExportsPlugin(typeof(NdmfPlugin))]

public class NdmfPlugin : Plugin<NdmfPlugin>
{
    sealed class BuildState
    {
        public DeferredObfuscation Obfuscation;
    }

    protected override void Configure()
    {
        InPhase(BuildPhase.Transforming).
            AfterPlugin("nadena.dev.modular-avatar").
            Run("Encrypting2", ctx =>
            {
                Debug.Log("After encrypting Shell Protector");
                var shellProtector = ctx.AvatarRootObject.GetComponentInChildren<ShellProtector>(true);
                if (shellProtector)
                    ctx.GetState<BuildState>().Obfuscation = shellProtector.EncryptWithoutObfuscation();
            });

        InPhase(BuildPhase.Optimizing).
            AfterPlugin("FermataFT.Editor.NDMFPlugin").
            AfterPlugin("FermataFT.Editor.FaceBlendShapeOptimizationPlugin").
            AfterPlugin("com.anatawa12.avatar-optimizer").
            Run("Obfuscating blendshapes", ctx =>
            {
                ctx.GetState<BuildState>().Obfuscation?.Run(ctx.AvatarRootObject);
            });
    }
}
#endif
#endif
