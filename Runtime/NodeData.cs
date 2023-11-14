using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.CustomData;
using Unity.MergeInstancingSystem.Job;
using Unity.MergeInstancingSystem.Render;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public class NodeData
    {
        [Serializable]
        public struct ListInfo
        {
            [SerializeField]
            public int head;
            [SerializeField]
            public int length;
        }
        [SerializeField]
        public long m_identifier;
        [SerializeField]
        public int m_meshIndex;
        /// <summary>
        /// 一个节点内的data可能不是连续的
        /// </summary>
        [SerializeField] 
        public List<ListInfo> m_RenderData;
        [SerializeField]
        public int subMeshIndex;
        [SerializeField]
        public bool m_castShadow;
        [SerializeField]
        public RendererQueue m_queue;
        [SerializeField]
        public int m_material;

        private NativeList<ListInfo> m_renderData;

        public MaterialPropertyBlock m_propretyBlock;
        
        [SerializeField]
        public bool m_NeedLightMap;

        public NativeArray<DAABB> m_boundBoxs;

        public int objCount;

        private bool useCullResult;

        public NativeList<ListInfo> cull_Result;
        public NativeList<ListInfo> shadowcull_Result;
        public NativeList<ListInfo> RenderData
        {
            get
            {
                if (useCullResult)
                {
                    return cull_Result;
                }
                return m_renderData;
            }
        }

        public NativeList<ListInfo> ShadowData
        {
            get
            {
                return shadowcull_Result;
            }
        }
        public unsafe void Init(InstanceData instanceData,NativeList<JobHandle> taskJobHandles)
        {
            useCullResult = false;
            m_renderData = m_RenderData.ToNativeList(Allocator.Persistent);
            cull_Result = new NativeList<ListInfo>(Allocator.Persistent);
            shadowcull_Result = new NativeList<ListInfo>(Allocator.Persistent);
            var mesh = instanceData.m_meshs[m_meshIndex];
            objCount = 0;
            for (int i = 0; i < RenderData.Length; i++)
            {
                objCount += RenderData[i].length;
            }

            m_boundBoxs = new NativeArray<DAABB>(objCount, Allocator.Persistent);
            var dataBoxInit = new DInitNodeDataBox();
            {
                fixed (Matrix4x4* ptr = instanceData.m_matrix_Worlds)
                {
                    dataBoxInit.localToWords = ptr;
                }
                dataBoxInit.dataIndex = m_renderData;
                dataBoxInit.meshAABB = mesh.bounds;
                dataBoxInit.highBoxs = m_boundBoxs;
            }
            taskJobHandles.Add(dataBoxInit.Schedule());
        }

        public unsafe void InitView(in NativeList<JobHandle> taskJobHandles,NativeArray<DPlane> planes)
        {
            useCullResult = true;
            cull_Result.Clear();
            TreeCullingJob treeCullingJob = new TreeCullingJob();
            treeCullingJob.planes = (DPlane*)planes.GetUnsafePtr();
            treeCullingJob.result = cull_Result;
            treeCullingJob.dataIndex = m_renderData;
            treeCullingJob.objectBounds = (DAABB*)m_boundBoxs.GetUnsafePtr();
            taskJobHandles.Add(treeCullingJob.Schedule());
        }

        public unsafe void InitViewWithShadow(in NativeList<JobHandle> taskJobHandles,float maxShadowDis)
        {
            shadowcull_Result.Clear();
            ShaodwCullJob shaodwCullJob = new ShaodwCullJob();
            {
                shaodwCullJob.result = shadowcull_Result;
                shaodwCullJob.objectBounds = (DAABB*)m_boundBoxs.GetUnsafePtr();
                shaodwCullJob.dataIndex = m_renderData;
                shaodwCullJob.cullDis = maxShadowDis;
                shaodwCullJob.cameraPos = CameraRecognizerManager.ActiveRecognizer.cameraPos;
            }
            taskJobHandles.Add(shaodwCullJob.Schedule());
        }
        public void ResetState()
        {
            useCullResult = false;
        }
        public void CopyData(InstanceData instanceData,in NativeList<JobHandle> taskJobHandles)
        {
            
        }
        public void Dispose()
        {
            m_renderData.Dispose();
            m_boundBoxs.Dispose();
            cull_Result.Dispose();
            shadowcull_Result.Dispose();
        }
        public void CreatePropretyBlock()
        {
            m_propretyBlock = new MaterialPropertyBlock();
        }
    }
}