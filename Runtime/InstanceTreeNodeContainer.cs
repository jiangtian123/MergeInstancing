using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public class InstanceTreeNodeContainer
    {
        [SerializeField] public List<InstanceTreeNode> m_treeNodes = new List<InstanceTreeNode>();
        
        public int Count
        {
            get => m_treeNodes.Count;
        }
        
        public int Add(InstanceTreeNode node)
        {
            int id = m_treeNodes.Count;
            m_treeNodes.Add(node);

            return id;
        }
        
        public InstanceTreeNode Get(int id)
        {
            var treenode = m_treeNodes[id];
            return treenode;
           
        }
        
        public void Remove(int id)
        {
            
        }
    }
}