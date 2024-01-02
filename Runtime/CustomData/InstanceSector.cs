using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.MergeInstancingSystem
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
                return m_subSectors.Length;
            }
        }
        /// <summary>
        /// 当前Lod在Prefab中前面有几个mesh
        /// </summary>
        [SerializeField]
        public int matrixBais;
        /// <summary>
        /// 同上，灯光数据的偏移量
        /// </summary>
        [SerializeField]
        public int lightBais;
        [SerializeField]
        public InstanceSubSector[] m_subSectors;
    }
}