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
        
        /// <summary>
        /// 每种Mesh在这个Lod中相对于prefab的位置偏移
        /// </summary>
        [SerializeField]
        public int[] m_prefabMatrix;
        
        //序列化时需要
        [NonSerialized] 
        public InstanceSubSector[] m_subSectors;
        
        [NonSerialized]
        public Matrix4x4[] m_meshMatrix;
    }
}