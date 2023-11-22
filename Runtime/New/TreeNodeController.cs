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
        public float m_cullDistance;
        [SerializeField]
        public bool m_useJob;
        [SerializeField]
        public TreeNode m_root;
        [SerializeField]
        public int m_jobBeginLevel;
        /// <summary>
        /// 存放树的list
        /// </summary>
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
        public bool m_useMotionvecter;
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
        #endregion

        #region Profiler
        
        UnityEngine.Profiling.CustomSampler ShadowCull;
        UnityEngine.Profiling.CustomSampler TreeUpdate;
        UnityEngine.Profiling.CustomSampler SubmitObj;
        UnityEngine.Profiling.CustomSampler RenderingObj;
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
                new NativeArray<Matrix4x4>(m_instanceData.m_gameObjectData.Count, Allocator.TempJob);
            for (int i = 0; i < m_instanceData.m_gameObjectData.Count; i++)
            {
                matrix4x4s[i] = m_instanceData.m_gameObjectData[i].m_LodData[0].originMatrix[0];
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

        protected override void UnRegiste()
        {
            m_jobGameJectData.Dispose();
            m_rendereringGamerobj.Dispose();
            m_instanceEle.Dispose();
            m_viewElements.Dispose();
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
        public override void UpDateTree(in DPlane* planes, in float3 cameraPos,in float4x4 matrixProj, in float preRelative, in NativeList<JobHandle> taskHandles)
        {
            TreeUpdate.Begin();
            m_root.Update(taskHandles, 0, planes, cameraPos,matrixProj,preRelative);
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
                prefab.DispatchSetup(dElement.m_lodLevel,m_instanceData.GetData(dElement.m_dataIndex),m_subSectors,isShadow);
            }
            SubmitObj.End();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void DispatchDraw(CommandBuffer cmdBuffer, in int passIndex,RenderQueue renderQueue, bool UseMotionVectors)
        {
            RenderingObj.Begin();
            foreach (var instanceSubSector in m_subSectors)
            {
                if(instanceSubSector.RenderCount == 0)continue;
                instanceSubSector.DispatchDraw(cmdBuffer,passIndex,renderQueue,m_useMotionvecter&&UseMotionVectors);
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
                if (instanceSubSector.RenderCount == 0) continue;
                instanceSubSector.DispatchDrawShadow(cmdBuffer, passIndex);
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
    }
}