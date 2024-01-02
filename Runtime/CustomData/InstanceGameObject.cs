using System;
using UnityEngine;

namespace Unity.MergeInstancingSystem
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
        /// 矩阵数据的索引
        /// </summary>
        [SerializeField]
        public int m_dataIndex;
        /// <summary>
        /// 光照数据的索引
        /// </summary>
        [SerializeField]
        public int m_lightDataIndex;
    }
}