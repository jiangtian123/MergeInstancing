using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.MergeInstancingSystem.CustomData;
using Unity.MergeInstancingSystem.Pool;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.New
{
    
    /// <summary>
    /// 每个Gameobject用来做剔除操作时的替代
    /// </summary>
    public struct DElement
    {
        /// <summary>
        /// 标记该元素属于哪个类型
        /// </summary>
        public int m_mark;
        /// <summary>
        /// 该元素是否通过视锥剔除
        /// </summary>
        public bool m_visible;
        /// <summary>
        /// 选择哪个Lod级别
        /// </summary>
        public int m_lodLevel;
        /// <summary>
        /// 剔除使用
        /// </summary>
        public DAABB m_bounds;
        /// <summary>
        /// 判断Lod级别使用
        /// </summary>
        public DSphere m_sphers;
        
        /// <summary>
        /// 存放矩阵和光照数据的Index
        /// </summary>
        public int m_dataIndex;
    }

    /// <summary>
    /// 因为一个
    /// </summary>
    public struct DGameObjectData
    {
        public Matrix4x4 originMatrix;
        public float lightMapIndex;
        public Vector4 lightMapOffest;
    }
   
}