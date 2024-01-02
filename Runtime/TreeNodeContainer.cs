using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public class TreeNodeContainer
    {
        [SerializeField] public List<TreeNode> m_treeNodes = new List<TreeNode>();
        public int Count
        {
            get => m_treeNodes.Count;
        }
        
        public int Add(TreeNode node)
        {
            int id = m_treeNodes.Count;
            m_treeNodes.Add(node);

            return id;
        }
        
        public TreeNode Get(int id)
        {
            var treenode = m_treeNodes[id];
            return treenode;
        }
        public void Remove(int id)
        {
            
        }
    }
}