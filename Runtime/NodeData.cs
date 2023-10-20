using System;
using System.Collections.Generic;
using Unity.MergeInstancingSystem.Render;
using UnityEngine;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public class NodeData
    {
        [Serializable]
        public struct ListInfo
        {
            [SerializeField]
            public int head;
            [SerializeField]
            public int length;
        }
        [SerializeField]
        public int m_identifier;
        [SerializeField]
        public int m_meshIndex;
        /// <summary>
        /// 一个节点内的data可能不是连续的
        /// </summary>
        [SerializeField] 
        public List<ListInfo> m_matrix4x4Data;
        [SerializeField]
        public List<ListInfo> m_GIData;
        [SerializeField]
        public int subMeshIndex;
        [SerializeField]
        public bool m_castShadow;
        [SerializeField]
        public RendererQueue m_queue;
        [SerializeField]
        public int m_material;
        
        
        public MaterialPropertyBlock m_propretyBlock;
        
        [SerializeField]
        public bool m_NeedLightMap;
        [SerializeField]
        public List<int> m_matPropretyData;

        public void CreatePropretyBlock()
        {
            m_propretyBlock = new MaterialPropertyBlock();
        }
    }
}