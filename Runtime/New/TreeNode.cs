using System.Collections.Generic;
using Unity.MergeInstancingSystem.CustomData;

namespace Unity.MergeInstancingSystem.New
{
    public class TreeNode
    {
        /// <summary>
        /// 做剔除和判断High还是Low使用，同时判断是否需要做精确剔除
        /// </summary>
        public DAABB m_Box;
        /// <summary>
        /// 树真正存放的地方
        /// </summary>
        public InstanceTreeNodeContainer m_container;
        /// <summary>
        /// 子树的索引
        /// </summary>
        public List<int> m_childTreeNodeIds;
        /// <summary>
        /// 当前节点在整棵树中的层级
        /// </summary>
        public int m_level;
        
        
    }
}