#if UNITY_EDITOR
using UnityEngine;

namespace Shell.Protector
{
    public sealed class Pipeline
    {
        public BuildResult Encrypt(BuildRequest request, BuildSettings settings)
        {
            if (request == null || request.Owner == null)
                return new BuildResult();

            request.Owner.ApplySettings(settings);
            GameObject avatar = request.Owner.EncryptLegacy(request.UseSmallMipTexture, request.IsModular);

            BuildResult result = request.Owner.CurrentBuildResult;
            result.Avatar = avatar;
            return result;
        }
    }
}
#endif
