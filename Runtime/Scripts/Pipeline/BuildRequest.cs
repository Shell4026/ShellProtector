#if UNITY_EDITOR
using VRC.SDK3.Avatars.Components;

namespace Shell.Protector
{
    public sealed class BuildRequest
    {
        public BuildRequest(ShellProtector owner, VRCAvatarDescriptor descriptor, bool useSmallMipTexture, bool isModular)
        {
            Owner = owner;
            Descriptor = descriptor;
            UseSmallMipTexture = useSmallMipTexture;
            IsModular = isModular;
        }

        public ShellProtector Owner { get; }
        public VRCAvatarDescriptor Descriptor { get; }
        public bool UseSmallMipTexture { get; }
        public bool IsModular { get; }
    }
}
#endif
