using System;
using System.Collections.Generic;
using Unity.MergeInstancingSystem.Render;
using UnityEngine;
using UnityEngine.Rendering;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace Unity.MergeInstancingSystem.Utils
{
    /// <summary>
    /// 一个OBJ最小的剔除单元
    /// </summary>
    public class MinGameObject
    {
        [NonSerialized]
        private int identifier;
        public int Identifier
        {
            get
            {
                return identifier; 
            }
        }
        public float? m_lightMapIndex;
        public UnityEngine.Matrix4x4 m_localtoworld;
        public Vector4? m_lightMapOffest;
        public MinGameObject(Renderer renderer,int subMeshIndex,bool useLightMap)
        {
            int m_renderHasde = renderer.GetHashCode();
            identifier = m_renderHasde*10 + subMeshIndex;
            m_lightMapIndex = null;
            m_lightMapOffest = null;
            if (useLightMap)
            {
                m_lightMapIndex = (float)renderer.lightmapIndex;
                m_lightMapOffest = renderer.lightmapScaleOffset;
            }
            m_localtoworld = renderer.localToWorldMatrix;
        }
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            return identifier == ((MinGameObject)obj).identifier;
        }
    }
}