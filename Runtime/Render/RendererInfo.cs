using System.Collections.Generic;
using Unity.MergeInstancingSystem.Pool;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.Render
{
    public enum RendererQueue
    {
        Opaque = 0,
        Transparent = 1,
    }

    public class RendererInfo : IRendererInstanceInfo
    {
        public bool CastShadow;
        /// <summary>
        /// 如果为True，使用LightMap
        /// </summary>
        public bool useLightMapOrLightProbe;
        public Mesh m_mesh;
        public Material m_mat;
        public PoolID m_poolID;
        public MaterialPropertyBlock m_instanceBlock;
        public RendererQueue m_queue;
        public int m_SubMeshIndex;
        public float m_CameraDis;
        public int m_renderCount = 0;
        #region GetData
        public Mesh GetMesh()
        {
            return m_mesh;
        }

        public float Dis
        {
            get
            {
                return m_CameraDis;
            }
        }
        public Material GetMaterial()
        {
            return m_mat;
        }
        public bool UseLightMapOrProbe
        {
            get
            {
                return useLightMapOrLightProbe;
            }
        }
        public int GetPoolCount()
        {
            return PoolManager.Instance.GetPoolCount(m_poolID.m_matrix4x4ID);
        }
        public List<Pool<Matrix4x4>> GetMatrix4x4()
        {
            return PoolManager.Instance.GetMatrix4X4s(m_poolID.m_matrix4x4ID);
        }
        public List<Pool<float>> GetlightMapIndex()
        {
            return PoolManager.Instance.GetFloats(m_poolID.m_lightMapIndexId);
        }

        public List<Pool<Vector4>> GetlightMapScaleOffset()
        {
            return PoolManager.Instance.GetVector4s(m_poolID.m_lightMapScaleOffsetID);
        }
        public  MaterialPropertyBlock GetMatpropretyBlock()
        {
            return m_instanceBlock;
        }
        public int GetSubMeshIndex()
        {
            return m_SubMeshIndex;
        }
        #endregion

        /// <summary>
        /// 每帧重置一次Pool
        /// </summary>
        public void Dispose()
        {
            PoolManager.Instance.DisposePool(m_poolID.m_matrix4x4ID);
            if (useLightMapOrLightProbe)
            {
                PoolManager.Instance.DisposePool(m_poolID.m_lightMapIndexId);
                PoolManager.Instance.DisposePool(m_poolID.m_lightMapScaleOffsetID);
            }
        }
    }
}