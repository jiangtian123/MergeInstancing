using System;
using UnityEngine;


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
        public DTransform m_localtoworld;
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

            m_localtoworld =  new DTransform(renderer.transform.position, renderer.transform.rotation,
                renderer.transform.lossyScale);
        }
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            return identifier == ((MinGameObject)obj).identifier;
        }
    }
}