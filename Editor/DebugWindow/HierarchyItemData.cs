using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Unity.MergeInstancingSystem.DebugWindow
{
    public class HierarchyItemData: ScriptableObject
    {
        public int Index;
        public InstanceTreeNode TreeNode;
        public string Label;
        public bool IsOpen;
    }
}