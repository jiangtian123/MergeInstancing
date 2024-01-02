using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.MergeInstancingSystem;
using Unity.MergeInstancingSystem.Pool;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
namespace Unity.MergeInstancingSystem
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

        [NonSerialized]
        public int renderObjectNumber = 0;
        

        [NonSerialized]
        public Material[] m_runMats;


        public const int MAXRENDERNUMBER = 1024;
        public List<Vector4> m_indexBuffer;
        public List<Vector4> m_tempBuffer;
        public int RenderCount
        {
            get
            {
                return GetPoolCount();
            }
        }
        public void Initialize()
        {
            m_indexBuffer = new List<Vector4>(sectionCount);
            m_tempBuffer = new List<Vector4>();
           // m_poolId = InitPool(sectionCount);
            propertyBlock = new MaterialPropertyBlock();
            m_runMats = new Material[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                m_runMats[i] = new Material(materials[i]);
            }
        }
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DispatchDraw(CommandBuffer cmdBuffer,InstanceData instanceData,in int passIndex,RenderQueue renderQueue)
        {
            if(m_Renderqueue != renderQueue)return;
            for (int i = 0; i < m_subMeshIndex.Length; i++)
            {
                Material material = m_runMats[m_subMeshIndex[i]];
                propertyBlock.Clear();
                propertyBlock.SetTexture(TreeNodeController.PropertyID.ObjectMatrixID,instanceData.m_matrixs);
                material.EnableKeyword("CUSTOM_INSTANCING_ON");
                if (useLightMap)
                {
                    propertyBlock.SetTexture(TreeNodeController.PropertyID.LightData,instanceData.m_lightMapOffest);
                    material.EnableKeyword("CUSTOM_LIGHTMAP_ON");
                }
                int startIndex = 0;
                int batchSize = MAXRENDERNUMBER;
                while (startIndex < renderObjectNumber)
                {
                    int endIndex = Mathf.Min(startIndex + batchSize, renderObjectNumber);
                    m_tempBuffer.AddRange(m_indexBuffer.GetRange(startIndex, endIndex - startIndex));
                    propertyBlock.SetVectorArray(TreeNodeController.PropertyID.InstanceIndexID, m_tempBuffer);
                    cmdBuffer.DrawMeshInstancedProcedural(m_mesh, m_subMeshIndex[i], material, passIndex,
                        renderObjectNumber, propertyBlock);
                    m_tempBuffer.Clear();
                    startIndex += batchSize;
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DispatchDrawShadow(CommandBuffer cmdBuffer,InstanceData instanceData,in int passIndex)
        {
            if(!m_castShadow)return;
            for (int i = 0; i < m_subMeshIndex.Length; i++)
            {
                Material material = m_runMats[m_subMeshIndex[i]];
                propertyBlock.Clear();
                propertyBlock.SetTexture(TreeNodeController.PropertyID.ObjectMatrixID,instanceData.m_matrixs);
                int startIndex = 0;
                int batchSize = MAXRENDERNUMBER;
                while (startIndex < renderObjectNumber)
                {
                    int endIndex = Mathf.Min(startIndex + batchSize, renderObjectNumber);
                    m_tempBuffer.AddRange(m_indexBuffer.GetRange(startIndex, endIndex - startIndex));
                    propertyBlock.SetVectorArray(TreeNodeController.PropertyID.InstanceIndexID, m_tempBuffer);
                    cmdBuffer.DrawMeshInstancedProcedural(m_mesh, m_subMeshIndex[i], material, passIndex,
                        renderObjectNumber, propertyBlock);
                    m_tempBuffer.Clear();
                    startIndex += batchSize;
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

        public void AddData(DGameObjectData data,LodSerializableData serializableData, bool isShadow)
        {
            renderObjectNumber++;
            PoolManager.Instance.AddData(m_poolId.m_matrix4x4ID,data.m_originMatrixIndex,serializableData.originMatrix);
            if (!isShadow && useLightMap)
            {
                PoolManager.Instance.AddData(m_poolId.m_lightMapIndexId,data.m_Lightindex,serializableData.lightmapIndex);
                PoolManager.Instance.AddData(m_poolId.m_lightMapScaleOffsetID,data.m_Lightindex,serializableData.lightmapOffest);
            }
        }

        public void AddData(float4 index)
        {
            renderObjectNumber++;
            m_indexBuffer.Add(index);
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
            int matHash = materials[0].GetHashCode();
            long indentif = meshHash + matHash;
            int thisHashCode = indentif.GetHashCode() << 2 | (useLightMap ? 0 : 1) << 1 | (m_castShadow ? 1 : 0);
            return thisHashCode;
        }
        public void ResetBuffer()
        {
            renderObjectNumber = 0;
            m_indexBuffer.Clear();
            // PoolManager.Instance.ResetPool<Matrix4x4>(m_poolId.m_matrix4x4ID);
            // if (useLightMap)
            // {
            //     PoolManager.Instance.ResetPool<float>(m_poolId.m_lightMapIndexId);
            //     PoolManager.Instance.ResetPool<Vector4>(m_poolId.m_lightMapScaleOffsetID);
            // }
        }
        public void Dispose()
        {
            m_indexBuffer = null;
            m_runMats = null;
            // PoolManager.Instance.ReleasePool<Matrix4x4>(m_poolId.m_matrix4x4ID);
            // if (useLightMap)
            // {
            //     PoolManager.Instance.ReleasePool<float>(m_poolId.m_lightMapIndexId);
            //     PoolManager.Instance.ReleasePool<Vector4>(m_poolId.m_lightMapScaleOffsetID);
            // }
        }
    }
}