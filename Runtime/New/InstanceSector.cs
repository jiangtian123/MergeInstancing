using System;
using UnityEngine;

namespace Unity.MergeInstancingSystem.New
{
    /// <summary>
    /// 一种Lod一个
    /// </summary>
    [Serializable]
    public class InstanceSector
    {
        public int MeshCount
        {
            get
            {
                return m_meshs.Length;
            }
        }
        /// <summary>
        /// 引用的Mesh类型
        /// </summary>
        [SerializeField]
        public int[] m_meshs;

        [NonSerialized] 
        public InstanceSubSector[] m_subSectors;
    }
}