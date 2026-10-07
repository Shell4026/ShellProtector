#if UNITY_EDITOR
using UnityEngine;

namespace Shell.Protector
{
    public sealed class BuildRequest
    {
        public BuildRequest(GameObject avatar, bool clone, ShellProtector owner = null)
        {
            Avatar = avatar;
            Clone = clone;
            Owner = owner;
        }

        // The avatar root, with the VRCAvatarDescriptor and the ShellProtector component.
        public GameObject Avatar { get; }
        // Encrypt a copy and disable the original (manual encryption). Otherwise the avatar itself is encrypted (NDMF upload).
        public bool Clone { get; }
        // What the ShellProtectorTester of a copy points to, so it can set the user key in the editor.
        public ShellProtector Owner { get; }
    }
}
#endif
