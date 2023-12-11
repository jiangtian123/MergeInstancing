using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.CustomData;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.New
{
    [Serializable]
    public unsafe class TreeNodeController : ControllerComponent
    {
        #region SerializableData
        [SerializeField]
        public bool m_useJob;
        [SerializeField]
        public TreeNode m_root;
        [SerializeField]
        public int m_jobBeginLevel;
        [SerializeField]
        public TreeNodeContainer m_treeNodeContainer;
        [SerializeField]
        public InstancePrefab[] m_instanceSector;
        [SerializeField]
        public InstanceGameObject[] m_gameobject;
        [SerializeField]
        public InstanceSubSector[] m_subSectors;
        [SerializeField]
        public InstanceData m_instanceData;
        [SerializeField]
        public bool m_castShadow;
        #endregion
        
        #region RunTimeData
        /// <summary>
        /// 树展成list后做Job剔除的数据
        /// </summary>
        [NonSerialized]
        public NativeArray<JobTreeData> m_jobGameJectData;
        /// <summary>
        /// 记录的是需要做剔除的gameobject
        /// </summary>
        [NonSerialized]
        public NativeList<int> m_rendereringGamerobj;
        /// <summary>
        /// 每个Gameobject对应一个DElement
        /// </summary>
        [NonSerialized]
        public NativeArray<DElement> m_instanceEle;
        [NonSerialized]
        public List<NativeArray<float>> m_lodInfos;
        [NonSerialized]
        public NativeArray<int> m_lodNumber;
        [NonSerialized]
        public NativeArray<IntPtr> m_lodPtr;
        /// <summary>
        /// 每个实例是否能被提交
        /// </summary>
        [NonSerialized]
        private NativeArray<int> m_viewElements;
        [NonSerialized]
        private ComputeBuffer m_OriginMatrixBuffer;
        [NonSerialized]
        private ComputeBuffer m_PrefabMatrixBuffer;
        [NonSerialized]
        private ComputeBuffer m_LightDataBuffer;
        #endregion

        #region Profiler
        [NonSerialized]
        UnityEngine.Profiling.CustomSampler ShadowCull;
        [NonSerialized]
        UnityEngine.Profiling.CustomSampler TreeUpdate;
        [NonSerialized]
        UnityEngine.Profiling.CustomSampler SubmitObj;
        [NonSerialized]
        UnityEngine.Profiling.CustomSampler RenderingObj;
        [NonSerialized]
        UnityEngine.Profiling.CustomSampler RenderingShadow;

        #endregion
        
        #region Unity Event

        protected override void OnRegiste()
        {
            ShadowCull = UnityEngine.Profiling.CustomSampler.Create("Shadow Cull");
            TreeUpdate = UnityEngine.Profiling.CustomSampler.Create("Tree Update");
            SubmitObj = UnityEngine.Profiling.CustomSampler.Create("Submit Object");
            RenderingObj = UnityEngine.Profiling.CustomSampler.Create("Rendering Object");
            RenderingShadow = UnityEngine.Profiling.CustomSampler.Create("Rendering Shadow");
            m_jobGameJectData = new NativeArray<JobTreeData>(m_treeNodeContainer.Count, Allocator.Persistent);
            m_rendereringGamerobj = new NativeList<int>(256, Allocator.Persistent);
            m_instanceEle = new NativeArray<DElement>(m_gameobject.Length, Allocator.Persistent);
            m_viewElements = new NativeArray<int>(m_treeNodeContainer.Count, Allocator.Persistent);
            m_lodInfos = new List<NativeArray<float>>();
            m_lodNumber = new NativeArray<int>(m_instanceSector.Length, Allocator.Persistent);
            m_lodPtr = new NativeArray<IntPtr>(m_instanceSector.Length, Allocator.Persistent);
            m_instanceData.Init();
            m_root.SetContainer(m_treeNodeContainer);
            m_root.Initialize(this);
            foreach (var subSector in m_subSectors)
            {
                subSector.Initialize();
            }
            InitJobTree();
            InitJobElement();
            InitLodData();
            InitComputeBuffer();
        }

        private void InitJobTree()
        {
            for (int i = 0; i < m_treeNodeContainer.m_treeNodes.Count; i++)
            {
                var node = m_treeNodeContainer.Get(i);
                m_jobGameJectData[i] = new JobTreeData(node.m_Box,node.m_childTreeNodeIds,node.m_Gameobj.head,node.m_Gameobj.number);
            }
        }

        private void InitJobElement()
        {
            HashSet<int> index = new HashSet<int>();
            for (int i = 0; i < m_gameobject.Length; i++)
            {
                var gameObject = m_gameobject[i];
                DElement element = new DElement();
                element.m_mark = gameObject.m_prefabIndex;
                index.Add(element.m_mark);
                element.m_lodLevel = 0;
                element.m_visible = false;
                element.m_dataIndex = gameObject.m_dataIndex;
                element.m_lightDataIndex = gameObject.m_lightDataIndex;
                m_instanceEle[i] = element;
            }

            NativeArray<DAABB> boxs = new NativeArray<DAABB>(m_instanceSector.Length, Allocator.TempJob);
            for (int i = 0; i < m_instanceSector.Length; i++)
            {
                boxs[i] = m_instanceSector[i].m_box;
            }

            InstanceScatterJob scatterJob = new InstanceScatterJob();
            scatterJob.treeElements = (DElement*)m_instanceEle.GetUnsafePtr();
            scatterJob.boxs = (DAABB*)boxs.GetUnsafePtr();
            NativeArray<Matrix4x4> matrix4x4s =
                new NativeArray<Matrix4x4>(m_instanceData.m_gameObjectMatrix.Count, Allocator.TempJob);
            for (int i = 0; i < m_instanceData.m_gameObjectMatrix.Count; i++)
            {
                matrix4x4s[i] = m_instanceData.m_gameObjectMatrix[i];
            }
            scatterJob.matrix = (Matrix4x4*)matrix4x4s.GetUnsafePtr();

            scatterJob.Schedule(m_instanceEle.Length, 64).Complete();
            matrix4x4s.Dispose();
            boxs.Dispose();
        }

        private void InitLodData()
        {
            for (int i = 0; i < m_instanceSector.Length; i++)
            {
                NativeArray<float> lodinfo = m_instanceSector[i].m_LODInfos.ToNativeArray(Allocator.Persistent);
                m_lodInfos.Add(lodinfo);
                m_lodNumber[i] = lodinfo.Length;
                m_lodPtr[i] = ((IntPtr)lodinfo.GetUnsafePtr());
            }
        }

        private void InitComputeBuffer()
        {
            m_OriginMatrixBuffer = new ComputeBuffer(m_instanceData.m_gameobjTransform.Count, sizeof(float) * 16*2);
            m_PrefabMatrixBuffer = new ComputeBuffer(m_instanceData.m_prefabMatrixs.Count, sizeof(float) * 16*2);
            m_LightDataBuffer = new ComputeBuffer(m_instanceData.m_lightData.Count+1, sizeof(InstanceLightData));
            
            NativeArray<MatrixWithInvMatrix> ObjectMatrix = new NativeArray<MatrixWithInvMatrix>(m_instanceData.m_gameobjTransform.Count,Allocator.TempJob);
            
            NativeArray<MatrixWithInvMatrix> meshMatrix = new NativeArray<MatrixWithInvMatrix>(m_instanceData.m_prefabMatrixs.Count,Allocator.TempJob);
            
            
            NativeArray<Matrix4x4> oM = m_instanceData.m_gameObjectMatrix.ToNativeArray(Allocator.TempJob);
            NativeArray<Matrix4x4> instanceMeshMatrix = m_instanceData.m_prefabMatrixs.ToNativeArray(Allocator.TempJob);
            NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(2, Allocator.TempJob);
            
            DCalculateMatrixInv instanceDataJob = new DCalculateMatrixInv();
            instanceDataJob.originMatrix = oM;
            instanceDataJob.matrix_Worlds = ObjectMatrix;
            
            
            DCalculateMatrixInv calculateMatrixInv = new DCalculateMatrixInv();
            calculateMatrixInv.originMatrix = instanceMeshMatrix;
            calculateMatrixInv.matrix_Worlds = meshMatrix;
            
            jobHandles[0] = instanceDataJob.Schedule(oM.Length, 64);
            jobHandles[1] = calculateMatrixInv.Schedule(instanceMeshMatrix.Length, 16);
            JobHandle.CompleteAll(jobHandles);
            m_OriginMatrixBuffer.SetData(ObjectMatrix);
            m_PrefabMatrixBuffer.SetData(meshMatrix);
            m_LightDataBuffer.SetData(m_instanceData.m_lightData);
            
            
            ObjectMatrix.Dispose();
            meshMatrix.Dispose();
            oM.Dispose();
            instanceMeshMatrix.Dispose();
            jobHandles.Dispose();
        }

        protected override void UnRegiste()
        {
            m_jobGameJectData.Dispose();
            m_rendereringGamerobj.Dispose();
            m_instanceEle.Dispose();
            m_viewElements.Dispose();
            m_OriginMatrixBuffer.Dispose();
            m_PrefabMatrixBuffer.Dispose();
            m_LightDataBuffer.Dispose();
            foreach (var VARIABLE in m_lodInfos)
            {
                VARIABLE.Dispose();
            }

            m_lodNumber.Dispose();
            m_lodPtr.Dispose();
            m_root.Dispose();
            foreach (var subSector in m_subSectors)
            {
                subSector.Dispose();
            }
        }

        #endregion
        
        #region Renderering

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void UpDateTreeWithShadow(in DPlane* planes,in NativeList<JobHandle> taskHandles)
        {
            if(!m_castShadow)return;
            ShadowCull.Begin();
            m_root.UpdateWithShadow(taskHandles, 0, planes);
            ShadowCull.End();
        }
        
        /// <summary>
        /// 遍历树，得到需要做剔除的Gameobject
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void UpDateTree(in DPlane* planes, in float3 cameraPos,in float4x4 matrixProj,in NativeList<JobHandle> taskHandles)
        {
            TreeUpdate.Begin();
            m_root.Update(taskHandles, 0, planes, cameraPos,matrixProj);
            TreeUpdate.End();
        }
        /// <summary>
        /// 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void InitView(in float3 cameraPos, in float4x4 matrixProj,in DPlane* planes,in NativeList<JobHandle> taskHandles)
        {
            
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void DispatchSetup(in NativeList<JobHandle> taskHandles,bool isShadow)
        {
            if(isShadow&&!m_castShadow)return;
            SubmitObj.Begin();
            foreach (var dElement in m_instanceEle)
            {
                if(!dElement.m_visible) continue;
                var prefab = m_instanceSector[dElement.m_mark];
                prefab.DispatchSetup(dElement.m_dataIndex, dElement.m_lightDataIndex, dElement.m_lodLevel, m_subSectors,
                    isShadow);
            }
            SubmitObj.End();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void DispatchDraw(CommandBuffer cmdBuffer, in int passIndex,RenderQueue renderQueue)
        {
            RenderingObj.Begin();
            foreach (var instanceSubSector in m_subSectors)
            {
                if(instanceSubSector.renderObjectNumber == 0)continue;
                instanceSubSector.DispatchDraw(cmdBuffer,m_OriginMatrixBuffer,m_PrefabMatrixBuffer,m_LightDataBuffer,passIndex,renderQueue);
            }
            RenderingObj.End();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void DispatchDrawShadow(CommandBuffer cmdBuffer, in int passIndex)
        {
            if(!m_castShadow)return;
            RenderingShadow.Begin();
            foreach (var instanceSubSector in m_subSectors)
            {
                if (instanceSubSector.renderObjectNumber == 0) continue;
                instanceSubSector.DispatchDrawShadow(cmdBuffer, m_OriginMatrixBuffer, m_PrefabMatrixBuffer, passIndex);
            }
            RenderingShadow.End();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Reset()
        {
            ResetElement();
            foreach (var instanceSubSector in m_subSectors)
            {
                instanceSubSector.ResetBuffer();
            }
        }
        private void ResetElement()
        {
            ResetElementState resetElementState = new ResetElementState();
            resetElementState.gameobject = (DElement*)m_instanceEle.GetUnsafePtr();
            resetElementState.Schedule(m_instanceEle.Length,64).Complete();
        }
        #endregion

        #region ShaderID

        public class PropertyID
        {
            public readonly static int ObjectMatrixID = Shader.PropertyToID("_ObjectMatrixs");
            public readonly static int MeshMatrixID = Shader.PropertyToID("_MeshMatrixs");
            public readonly static int LightDataID = Shader.PropertyToID("_InstanceLightDatas");
            public readonly static int InstanceIndexID = Shader.PropertyToID("_InstanceIndex");
        }
        #endregion
    }
}