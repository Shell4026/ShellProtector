#if UNITY_EDITOR
using UnityEngine;

namespace Shell.Protector
{
    public sealed class ShellProtectorPipeline
    {
        public ShellProtectorBuildResult Encrypt(ShellProtectorBuildRequest request, ShellProtectorSettings settings)
        {
            if (request == null || request.Owner == null)
                return new ShellProtectorBuildResult();

            request.Owner.ApplySettings(settings);
            GameObject avatar = request.Owner.EncryptLegacy(request.UseSmallMipTexture, request.IsModular);

            ShellProtectorBuildResult result = request.Owner.CurrentBuildResult;
            result.Avatar = avatar;
            return result;
        }
    }
}
#endif
