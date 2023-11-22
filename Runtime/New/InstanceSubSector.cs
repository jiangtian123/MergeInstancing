using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.MergeInstancingSystem.CustomData;
using Unity.MergeInstancingSystem.Pool;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
namespace Unity.MergeInstancingSystem.New
{
     /// <summary>
    /// 渲染级别的数据
    /// </summary>
    [Serializable]
    public class InstanceSubSector :ScriptableObject
    {
        [SerializeField]
        public Mesh m_mesh;
        [SerializeField]
        //当前ID对应的Mesh有多少种SubMesh
        public int[] m_subMeshIndex;
        [SerializeField]
        //每种SubMesh对应的材质ID
        public Material[] materials;
        [NonSerialized]
        public PoolID m_poolId;
        [NonSerialized]
        private MaterialPropertyBlock propertyBlock;
        [SerializeField]
        public bool useLightMap;

        [SerializeField] 
        public RenderQueue m_Renderqueue;

        [SerializeField] 
        public int sectionCount;
        [SerializeField] 
        public bool m_castShadow;

        public int RenderCount
        {
            get
            {
                return GetPoolCount();
            }
        }
        
        
        public void Initialize()
        {
            m_poolId = InitPool(sectionCount);
            propertyBlock = new MaterialPropertyBlock();
        }
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DispatchDraw(CommandBuffer cmdBuffer,in int passIndex,RenderQueue renderQueue,bool UseMotionVectors)
        {
            if(m_Renderqueue != renderQueue)return;
            if (useLightMap)
            {
                var matrix4x4pool = GetMatrix4x4();
                var lightMapIndexpool = GetlightMapIndex();
                var lightScalOffsetpool = GetlightMapScaleOffset();
                var poolCountcount = GetPoolCount();
                for (int renderCount = 0; renderCount < poolCountcount; renderCount++)
                {
                    var matrixs = matrix4x4pool[renderCount].OnePool;
                    var lightmapIndex = lightMapIndexpool[renderCount].OnePool;
                    var ligtmapOffest = lightScalOffsetpool[renderCount].OnePool;
                    var count = matrix4x4pool[renderCount].length;
                    for (int i = 0; i < m_subMeshIndex.Length; i++)
                    {
                        Material material = materials[m_subMeshIndex[i]];
                        propertyBlock.Clear();
                        propertyBlock.SetFloatArray("_lightMapIndex", lightmapIndex);
                        propertyBlock.SetVectorArray("_LightScaleOffset", ligtmapOffest);
                        material.shaderKeywords = null;
                        material.EnableKeyword("CUSTOM_LIGHTMAP_ON");
                        material.EnableKeyword("CUSTOM_INSTANCING_ON");
                        cmdBuffer.DrawMeshInstanced(m_mesh, m_subMeshIndex[i], material, passIndex, matrixs, count,
                            propertyBlock);
                    }
                }
            }
            else 
            {
                var matrix4x4pool = GetMatrix4x4();
                var poolCountcount = GetPoolCount();
                for (int renderCount = 0; renderCount < poolCountcount; renderCount++)
                {
                    var matrixs = matrix4x4pool[renderCount].OnePool;
                    var count = matrix4x4pool[renderCount].length;
                    for (int i = 0; i < m_subMeshIndex.Length; i++)
                    {
                        Material material = materials[m_subMeshIndex[i]];
                        propertyBlock.Clear();
                        material.shaderKeywords = null;
                        material.EnableKeyword("CUSTOM_INSTANCING_ON");
                        cmdBuffer.DrawMeshInstanced(m_mesh, m_subMeshIndex[i], material, passIndex, matrixs, count,
                            propertyBlock);
                    }
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DispatchDrawShadow(CommandBuffer cmdBuffer,in int passIndex)
        {
            if(!m_castShadow)return;
            var matrix4x4pool = GetMatrix4x4();
            var poolCountcount = GetPoolCount();
            for (int renderCount = 0; renderCount < poolCountcount; renderCount++)
            {
                var matrixs = matrix4x4pool[renderCount].OnePool;
                var count = matrix4x4pool[renderCount].length;
                for (int i = 0; i < m_subMeshIndex.Length; i++)
                {
                    Material material = materials[m_subMeshIndex[i]];
                    propertyBlock.Clear();
                    cmdBuffer.DrawMeshInstanced(m_mesh, m_subMeshIndex[i], material, passIndex, matrixs, count,
                        propertyBlock);
                }
            }
        }
        public int GetPoolCount()
        {
            return PoolManager.Instance.GetPoolCount<Matrix4x4>(m_poolId.m_matrix4x4ID);
        }
        public List<Pool<Matrix4x4>> GetMatrix4x4()
        {
            return PoolManager.Instance.GetData<Matrix4x4>(m_poolId.m_matrix4x4ID);
        }
        public List<Pool<float>> GetlightMapIndex()
        {
            return PoolManager.Instance.GetData<float>(m_poolId.m_lightMapIndexId);
        }
        public List<Pool<Vector4>> GetlightMapScaleOffset()
        {
            return PoolManager.Instance.GetData<Vector4>(m_poolId.m_lightMapScaleOffsetID);
        }
        public PoolID InitPool(int sectionCount)
        {
            PoolID ID = new PoolID();
            ID.m_matrix4x4ID = PoolManager.Instance.AllocatPool<Matrix4x4>(sectionCount);
            if (useLightMap)
            {
                ID.m_lightMapIndexId = PoolManager.Instance.AllocatPool<float>(sectionCount);
                ID.m_lightMapScaleOffsetID = PoolManager.Instance.AllocatPool<Vector4>(sectionCount);
            }
            return ID;
        }

        public void AddData(DGameObjectData data,bool isShadow)
        {
            PoolManager.Instance.AddData(m_poolId.m_matrix4x4ID, data.originMatrix);
            if (!isShadow && useLightMap)
            {
                PoolManager.Instance.AddData(m_poolId.m_lightMapIndexId, data.lightMapIndex.Value);
                PoolManager.Instance.AddData(m_poolId.m_lightMapScaleOffsetID, data.lightMapOffest.Value);
            }
        }
        
        public bool Equals(InstanceSubSector target)
        {
            return target.GetHashCode() == this.GetHashCode();
        }

        public override bool Equals(object target)
        {
            return Equals((InstanceSubSector)target);
        }
        public override int GetHashCode()
        {
            int meshHash = m_mesh.GetHashCode();
            int matHash = materials.GetHashCode();
            long indentif = meshHash + matHash;
            int thisHashCode = indentif.GetHashCode() << 2 | (useLightMap ? 0 : 1) << 1 | (m_castShadow ? 1 : 0);
            return thisHashCode;
        }
        public void ResetBuffer()
        {
            PoolManager.Instance.ResetPool<Matrix4x4>(m_poolId.m_matrix4x4ID);
            if (useLightMap)
            {
                PoolManager.Instance.ResetPool<float>(m_poolId.m_lightMapIndexId);
                PoolManager.Instance.ResetPool<Vector4>(m_poolId.m_lightMapScaleOffsetID);
            }
        }
        public void Dispose()
        {
            PoolManager.Instance.ReleasePool<Matrix4x4>(m_poolId.m_matrix4x4ID);
            if (useLightMap)
            {
                PoolManager.Instance.ReleasePool<float>(m_poolId.m_lightMapIndexId);
                PoolManager.Instance.ReleasePool<Vector4>(m_poolId.m_lightMapScaleOffsetID);
            }
        }
    }
}