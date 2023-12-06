using System;
using UnityEngine;

namespace Unity.MergeInstancingSystem.New
{
    /// <summary>
    /// 场景中的gameobject的代理
    /// </summary>
    [Serializable]
    public class InstanceGameObject
    {
        /// <summary>
        /// 对应的预制体在数组的哪个位置
        /// </summary>
        [SerializeField]
        public int m_prefabIndex;
        /// <summary>
        /// 对应的矩阵和光照数据的地方
        /// </summary>
        [SerializeField]
        public int m_dataIndex;
        
        [SerializeField]
        public int m_lightDataIndex;
    }
}