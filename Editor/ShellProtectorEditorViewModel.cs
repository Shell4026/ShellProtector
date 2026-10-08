#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Shell.Protector
{
    internal sealed class ShellProtectorEditorViewModel
    {
        readonly ShellProtector protector;
        readonly SerializedProperty syncSize;
        readonly ReorderableList gameobjectList;
        readonly ReorderableList materialList;

        public ShellProtectorEditorViewModel(
            ShellProtector protector,
            SerializedProperty syncSize,
            ReorderableList gameobjectList,
            ReorderableList materialList)
        {
            this.protector = protector;
            this.syncSize = syncSize;
            this.gameobjectList = gameobjectList;
            this.materialList = materialList;
        }

        public bool HasParameterAsset { get; private set; }
        public int FreeParameter { get; private set; }
        public int UsedParameter { get; private set; }
        public bool HasEnoughParameterSpace => HasParameterAsset && FreeParameter >= UsedParameter;
        public bool HasTargets => gameobjectList.count > 0 || materialList.count > 0;

        public void Refresh()
        {
            VRCExpressionParameters parameters = protector.GetParameter();
            HasParameterAsset = parameters != null;
            FreeParameter = HasParameterAsset ? 256 - parameters.CalcTotalCost() : -1;

            int lockSize = 1;
            int switchCount = ShellProtector.GetRequiredSwitchCount(ShellProtector.KeySize, syncSize.intValue);
            UsedParameter = switchCount + lockSize + syncSize.intValue * 8;
        }
    }
}
#endif
