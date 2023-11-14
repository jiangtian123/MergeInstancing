using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.CustomData;
using Unity.MergeInstancingSystem.Job;
using Unity.MergeInstancingSystem.Pool;
using Unity.MergeInstancingSystem.Render;
using Unity.MergeInstancingSystem.SpaceManager;
using Unity.MergeInstancingSystem.Utils;
using UnityEngine;
using UnityEditor;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Unity.MergeInstancingSystem.Controller
{
    public abstract class InstanceControllerBase :MonoBehaviour, ISerializationCallbackReceiver
    {
        public abstract void Install();
        
        public abstract void OnStart();
        public abstract void OnStop();
        
       
        #region Unity Events
        
        public void Awake()
        {
            m_spaceManager = new QuadTreeSpaceManager();
            m_positionPoolMap = new Dictionary<long, PoolID>();
            m_renderInfo = new Dictionary<long, RendererInfo>();
            m_shadowInfo = new Dictionary<long, RendererInfo>();
            m_shadowPositionPoolMap = new Dictionary<long, PoolID>();
        }
        
        public void Start()
        {
            UpdateSampler = UnityEngine.Profiling.CustomSampler.Create("Begin Update Tree");
            ResetState = UnityEngine.Profiling.CustomSampler.Create("Reset State");
            Shadow = UnityEngine.Profiling.CustomSampler.Create("Shadow");
            UpdateTreeNode = UnityEngine.Profiling.CustomSampler.Create("Update Tree Node");
            Renderering = UnityEngine.Profiling.CustomSampler.Create("Renderering");
        }
        /*
         * 分配内存的地方
         * 
         */
        public PoolID InitPoolWithInstanceData(int count,bool useLightOrLightProbe)
        {
            PoolID ID = new PoolID();
            ID.m_matrix4x4ID = PoolManager.Instance.AllocatMatrix4x4Pool(count);
            if (useLightOrLightProbe)
            {
                ID.m_lightMapIndexId = PoolManager.Instance.AllocatFloatPool(count);
                ID.m_lightMapScaleOffsetID = PoolManager.Instance.AllocatVector4Pool(count);
            }
            return ID;
        }
        
        
        public void OnEnable()
        {
            InstanceManager.Instance.Register(this);
            m_instanceData.Init();
            InitDotsData();
            m_root.Initialize(this, m_spaceManager, null);
            InitShadowDotsData();
            //
            // ------------------- 申请内存 ----------------------------------------
            try
            {
                AllocatorPool();
                if (castShadow)
                {
                    AllocatorShadowPool();
                }
                
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
           
        }

        private void AllocatorPool()
        {
            // m_matAndMeshIdentifiers : 每种Mesh-Mat搭配的唯一标识符
            var renderClassStates = m_instanceData.m_renderClass;
            for (int i = 0; i < renderClassStates.Count; i++)
            {
                //Count 是该批次有多少个OBJ
                var count = renderClassStates[i].m_citations;
                var identifier = renderClassStates[i].m_identifier;
                bool useLightMapOrProbe = renderClassStates[i].m_useLightMap;
                var id =  InitPoolWithInstanceData(count,useLightMapOrProbe);
                m_positionPoolMap.Add(identifier,id);
            }
        }
        private void AllocatorShadowPool()
        {
            foreach (var nodeData in m_root.LowObjects)
            {
                int count = nodeData.objCount;
                var identifier = nodeData.m_identifier;
                var id =  InitPoolWithInstanceData(count,false);
                m_shadowPositionPoolMap.Add(identifier,id);
            }
        }
        public void OnDisable()
        {
            m_renderInfo.Clear();
            m_root.Dispose();
            DisposeNativeArray();
            foreach (var poolID in m_positionPoolMap)
            {
                var id = poolID.Value;
                PoolManager.Instance.ReleasePool(id.m_matrix4x4ID);
                PoolManager.Instance.ReleasePool(id.m_lightMapIndexId);
                PoolManager.Instance.ReleasePool(id.m_lightMapScaleOffsetID);
            }
            m_positionPoolMap.Clear();
            InstanceManager.Instance.Unregister(this);
        }

        public void OnDestroy()
        {
            InstanceManager.Instance.Unregister(this);
        }
        
        #endregion
        #region variables
        
        /// <summary>
        /// 每种类型的DC对应一个Poll,需要在disable的时候清理
        /// </summary>
        private Dictionary<long, PoolID> m_positionPoolMap;
        
        /// <summary>
        /// 每种渲染类型对应的渲染数据,渲染类型指的是Mesh+Mat的唯一组合,需要每帧清理
        /// </summary>
        private Dictionary<long, RendererInfo> m_renderInfo;
        
        private Dictionary<long, PoolID> m_shadowPositionPoolMap;
        private Dictionary<long, RendererInfo> m_shadowInfo;
        private ISpaceManager m_spaceManager;

        public NativeArray<SmallTreeNode> smallTreeNodes;
        public NativeArray<byte> nodesState;
        public NativeList<JobHandle> JobHandles;
        public NativeList<JobHandle> shadowJobHandles;
        [SerializeField]
        private int m_controllerID;
        
        [SerializeField]
        public bool useMotionVector;
        /// <summary>
        /// 开启后，会对不完全在视锥体内的包围盒做精细剔除
        /// </summary>
        [SerializeField]
        public bool usePreciseCulling;
        
        [SerializeField] 
        private InstanceTreeNodeContainer m_treeNodeContainer;
        
        /// <summary>
        /// 四叉树的根节点
        /// </summary>
        [SerializeField]
        private InstanceTreeNode m_root;
        
        [SerializeField]
        private float maxShadowDis;

        [SerializeField] private bool castShadow;
        
        /// <summary>
        /// 剔除的距离
        /// </summary>
        
        [SerializeField] private float m_cullDistance;
        
        /// <summary>
        /// lod的比例
        /// </summary>
        [SerializeField] private float m_lodDistance;
        
        /// <summary>
        /// 渲染数据的存放地
        /// </summary>

        [SerializeField] private InstanceData m_instanceData;

        [SerializeField] private int shadowCullLevel;
        
        UnityEngine.Profiling.CustomSampler UpdateSampler;
        UnityEngine.Profiling.CustomSampler ResetState;
        UnityEngine.Profiling.CustomSampler Shadow;
        UnityEngine.Profiling.CustomSampler UpdateTreeNode;
        UnityEngine.Profiling.CustomSampler Renderering;

        public int ControllerID
        {
            set { m_controllerID = value;}
            get { return m_controllerID; }
        }
        
        public InstanceTreeNodeContainer Container
        {
            set
            {
                m_treeNodeContainer = value; 
                UpdateContainer();
            }
            get { return m_treeNodeContainer; }
        }
        
        public InstanceTreeNode Root
        {
            set
            {
                m_root = value; 
                UpdateContainer();
            }
            get { return m_root; }
        }

        public float MaxShadowDis
        {
            get
            {
                return maxShadowDis;
            }
            set
            {
                maxShadowDis = value;
            }
        }
        public InstanceData InstanceData
        {
            set
            {
                m_instanceData = value;
            }
            get
            {
                return m_instanceData;
            }
        }
        public float CullDistance
        {
            set { m_cullDistance = value; }
            get { return m_cullDistance; }
        }
        
        public float LODDistance
        {
            set { m_lodDistance = value; }
            get { return m_lodDistance; }
        }
        #endregion
        
        #region Method
        /// <summary>
        /// 
        /// </summary>
        /// <param name="rendererObj"></param>
        /// <param name="dis">当前节点距离相机的位置</param>
        public void RecordInstance(List<NodeData> rendererObj,bool usePreciseCulling = false)
        {
            try
            {
                for (int i = 0; i < rendererObj.Count; i++)
                {
                    var nodeData = rendererObj[i];
                    var mesh = m_instanceData.m_meshs[nodeData.m_meshIndex];
                    var mat = m_instanceData.m_materials[nodeData.m_material];
                    long identifier = nodeData.m_identifier;
                    if (m_renderInfo.TryGetValue(identifier,out var rendererInfo))
                    {
                        
                        AddDataToPool(nodeData,rendererInfo,usePreciseCulling);
                    }
                    else
                    {
                        RendererInfo tempInfo = new RendererInfo();
                        tempInfo.m_mat = mat;
                        tempInfo.m_mesh = mesh;
                        tempInfo.m_poolID = m_positionPoolMap[identifier];
                        tempInfo.CastShadow = nodeData.m_castShadow;
                        tempInfo.m_queue = nodeData.m_queue;
                        tempInfo.m_SubMeshIndex = nodeData.subMeshIndex;
                        tempInfo.useLightMapOrLightProbe = nodeData.m_NeedLightMap;
                        tempInfo.m_instanceBlock = nodeData.m_propretyBlock;
                        AddDataToPool(nodeData, tempInfo,usePreciseCulling);
                        m_renderInfo.Add(identifier,tempInfo);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
           
        }

        public void RecordInstanceWihtShadow(List<NodeData> rendererObj)
        {
            for (int i = 0; i < rendererObj.Count; i++)
            {
                var nodeData = rendererObj[i];
                var mesh = m_instanceData.m_meshs[nodeData.m_meshIndex];
                var mat = m_instanceData.m_materials[nodeData.m_material];
                long identifier = nodeData.m_identifier;
                if (m_shadowInfo.TryGetValue(identifier,out var rendererInfo))
                {
                    AddShadowDataToPool(nodeData,rendererInfo);
                }
                else
                {
                    RendererInfo tempInfo = new RendererInfo();
                    tempInfo.m_mat = mat;
                    tempInfo.m_mesh = mesh;
                    tempInfo.m_poolID = m_shadowPositionPoolMap[identifier];
                    tempInfo.CastShadow = nodeData.m_castShadow;
                    tempInfo.m_queue = nodeData.m_queue;
                    tempInfo.m_SubMeshIndex = nodeData.subMeshIndex;
                    tempInfo.useLightMapOrLightProbe = nodeData.m_NeedLightMap;
                    tempInfo.m_instanceBlock = nodeData.m_propretyBlock;
                    AddShadowDataToPool(nodeData, tempInfo);
                    m_shadowInfo.Add(identifier,tempInfo);
                }
            }
        }

        private void AddShadowDataToPool(NodeData nodeData,RendererInfo rendererInfo)
        {
            var poolId = rendererInfo.m_poolID;
            var Matrix4x4Source = m_instanceData.m_matrix_Worlds;
            var renderDataList = nodeData.ShadowData;
            for (int j = 0; j < renderDataList.Length; j++)
            {
                var listInfo = renderDataList[j];
                int head = listInfo.head;
                int length = listInfo.length;
                rendererInfo.m_renderCount += length;
                PoolManager.Instance.CopyData(poolId.m_matrix4x4ID, Matrix4x4Source, head, length);
            }
        }
        private void InitShadowDotsData()
        {
            shadowJobHandles = new NativeList<JobHandle>(Allocator.Persistent);
        }
        private void InitDotsData()
        {
            nodesState = new NativeArray<byte>(m_treeNodeContainer.m_treeNodes.Count + 1, Allocator.Persistent);
            smallTreeNodes = new NativeArray<SmallTreeNode>(m_treeNodeContainer.m_treeNodes.Count, Allocator.Persistent);
            JobHandles = new NativeList<JobHandle>(128, Allocator.Persistent);
            for (int i = 0; i < m_treeNodeContainer.m_treeNodes.Count; i++)
            {
                var node = m_treeNodeContainer.Get(i);
                smallTreeNodes[i] = new SmallTreeNode(node.HighCullBounds,node.LowCullBounds,node.m_childTreeNodeIds);
            }
        }

        private void DisposeNativeArray()
        {
            nodesState.Dispose();
            smallTreeNodes.Dispose();
            JobHandles.Dispose();
            // m_shadowTreeNode.Dispose();
            // m_shadowNodeState.Dispose();
            shadowJobHandles.Dispose();
        }
        
        /// <summary>
        /// 向事先申请好的内存中添加要渲染的数据
        /// </summary>
        /// <param name="nodeData">节点保存的数据信息</param>
        /// <param name="rendererInfo"></param>
        /// <param name="cullOBJ">是否挨个剔除节点中的OBJ</param>
        private void AddDataToPool(NodeData nodeData,RendererInfo rendererInfo,bool usePreciseCulling)
        {
            var poolId = rendererInfo.m_poolID;
            var lightMapIndexSource = m_instanceData.m_LightMapIndexsData;
            var lightMapScaleOffsetSource = m_instanceData.m_LightMapOffsetsData;
            var Matrix4x4Source = m_instanceData.m_matrix_Worlds;
            var renderDataList = nodeData.RenderData;
            nodeData.ResetState();
            for (int j = 0; j < renderDataList.Length; j++)
            {
                if (usePreciseCulling)
                {
                    var listInfo = renderDataList[j];
                    int head = listInfo.head;
                    int length = 0;
                    for (int i = 0; i < listInfo.length; i++)
                    {
                        float2 distRadius = new float2(0, 0);
                        var box = nodeData.m_boundBoxs[i + j];
                        bool isPass = true;
                        for (int PlaneIndex = 0; PlaneIndex < 6; ++PlaneIndex)
                        {
                            DPlane plane = CameraRecognizerManager.ActiveRecognizer.planes[PlaneIndex];
                            distRadius.x = math.dot(plane.normalDist.xyz, box.center) + plane.normalDist.w;
                            distRadius.y = math.dot(math.abs(plane.normalDist.xyz), box.extents);
                            if (distRadius.x + distRadius.y < 0)
                            {
                                isPass = false;
                                break;
                            }
                        }
                        if (isPass)
                        {
                            length += 1;
                        }
                        else if (length == 0)
                        {
                            head++;
                        }

                        if ((i + 1 == listInfo.length || !isPass) && length != 0)
                        {
                            rendererInfo.m_renderCount += length;
                            PoolManager.Instance.CopyData(poolId.m_matrix4x4ID, Matrix4x4Source, head, length);
                            if (nodeData.m_NeedLightMap && head >= 0)
                            {
                                PoolManager.Instance.CopyData(poolId.m_lightMapIndexId, lightMapIndexSource, head,
                                    length);
                                PoolManager.Instance.CopyData(poolId.m_lightMapScaleOffsetID, lightMapScaleOffsetSource,
                                    head,
                                    length);
                            }
                            head += (length + 1);
                            length = 0;
                        }
                    }
                }
                else
                {
                    var listInfo = renderDataList[j];
                    int head = listInfo.head;
                    int length = listInfo.length;
                    rendererInfo.m_renderCount += length;
                    PoolManager.Instance.CopyData(poolId.m_matrix4x4ID, Matrix4x4Source, head, length);
                    if (nodeData.m_NeedLightMap && head >= 0)
                    {
                        PoolManager.Instance.CopyData(poolId.m_lightMapIndexId, lightMapIndexSource, head, length);
                        PoolManager.Instance.CopyData(poolId.m_lightMapScaleOffsetID, lightMapScaleOffsetSource, head,
                            length);
                    }
                }
            }
        }
        //----------- 
        /// <summary>
        /// 更新四叉树
        /// </summary>
        /// <param name="camera"></param>
        public void UpdateCull(Camera camera)
        {
            UpdateSampler.Begin();
            if (m_spaceManager == null)
                return;
            m_spaceManager.UpdateCamera(this.transform, camera);
            //m_root.Update(m_lodDistance);
            // UpdateTree();
            //将一课树需要渲染的注册一下
            ResetState.Begin();
            JobHandles.Clear();
            ResetStateArray();
            ResetState.End();
            Shadow.Begin();
            if (castShadow)
            {
                ResiterShadow();
            }
            Shadow.End();
            UpdateTreeNode.Begin();
            m_root.UpdateWithJob(JobHandles,true,1,0);
            JobHandle.CompleteAll(JobHandles);
            UpdateTreeNode.End();
            Renderering.Begin();
            m_root.RegsiterRender(0,nodesState,usePreciseCulling);
            RegsiterRender();
            Renderering.End();
            UpdateSampler.End();
        }

        private void ResetStateArray()
        {
            ResetStateJob reset = new ResetStateJob();
            {
                reset.restArray = nodesState;
            }
            reset.Schedule(nodesState.Length, 64).Complete();
        }
        
        private void ResiterShadow()
        {
            foreach (var nodeData in m_root.LowObjects)
            {
                nodeData.InitViewWithShadow(shadowJobHandles,maxShadowDis);
            }
            
            JobHandle.CompleteAll(shadowJobHandles);
            RecordInstanceWihtShadow(m_root.LowObjects);
        }
        private void RegsiterRender()
        {
            foreach (var rendererInfo in m_renderInfo)
            {
                var info = rendererInfo.Value;
                if (info.m_renderCount == 0)
                {
                    continue;
                }
                switch (info.m_queue)
                {
                    case RendererQueue.Opaque:
                        InstanceManager.Instance.RegisterOpaqueRenderlist(info);
                        break;
                    case RendererQueue.Transparent:
                        InstanceManager.Instance.RegisterTransparentRenderlist(info);
                        break;
                }
            }
            foreach (var rendererInfo in m_shadowInfo)
            {
                var info = rendererInfo.Value;
                if (info.m_renderCount == 0)
                {
                    continue;
                }
                InstanceManager.Instance.RegisterShadowRenderlist(info);
            }
        }
        public void ResetRenderInfo()
        {
            foreach (var rendererInfo in m_renderInfo)
            {
                rendererInfo.Value.ResetPool();
            }
            foreach (var rendererInfo in m_shadowInfo)
            {
                rendererInfo.Value.ResetPool();
            }
        }
        
        public void OnBeforeSerialize()
        {
            
        }

        public void OnAfterDeserialize()
        {
            UpdateContainer();
        }
        private void UpdateContainer()
        {
            if (m_root == null)
                return;
            
            m_root.SetContainer(m_treeNodeContainer);
        }
        #endregion

#if UNITY_EDITOR
        #region Debug
        public int index = 0;
        [EasyButtons.Button]
        public void CalculateVS()
        {
            var node = Container.m_treeNodes[index];
            int HighVSCount = 0;
            int LowVSCount = 0;
            foreach (var nodeData in node.HighObjects)
            {
                int meshIndex = nodeData.m_meshIndex;
                int meshCount = 0;
                for (int i = 0; i < nodeData.m_RenderData.Count; i++)
                {
                    meshCount += nodeData.m_RenderData[i].length;
                }
                var mesh = m_instanceData.m_meshs[meshIndex];
                HighVSCount += mesh.vertexCount * meshCount;
            }
            foreach (var nodeData in node.LowObjects)
            {
                int meshIndex = nodeData.m_meshIndex;
                int meshCount = 0;
                for (int i = 0; i < nodeData.m_RenderData.Count; i++)
                {
                    meshCount += nodeData.m_RenderData[i].length;
                }
                var mesh = m_instanceData.m_meshs[meshIndex];
                LowVSCount += mesh.vertexCount * meshCount;
            }
            EditorUtility.DisplayDialog("结果", $"HIGH VS 个数为{HighVSCount}, LOW VS 个数为 {LowVSCount}", "确定");
        }
        // ---------------------- debug 用的 -----------------------------------------------------------------
        static readonly Color[] k_DepthColors = new Color[]
        {
            Color.red,
            Color.green,
            Color.blue,
            Color.magenta,
            Color.yellow,
            Color.cyan,
            Color.grey,
        };
        public static Color GetDepthColor(int depth)
        {
            return k_DepthColors[depth % k_DepthColors.Length];
        }
        void DrawGizmos(Bounds bounds, float alpha, Color color)
        {
            color.a = alpha;
            Gizmos.color = color;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
        void OnDrawGizmos()
        {
            var root = m_root;
            Queue<InstanceTreeNode> lis = new Queue<InstanceTreeNode>();
            lis.Enqueue(root);
            while (lis.Count>0)
            {
                var node = lis.Dequeue();
                for (int i = 0; i < node.GetChildTreeNodeCount(); i++)
                {
                    var no = node.GetChildTreeNode(i);
                    lis.Enqueue(no);
                }
                var depth = node.Level;
                DrawGizmos(node.Bounds, Mathf.Max(1f - Mathf.Pow(0.9f, depth), 0.2f), GetDepthColor(depth));
            }

            DnBugCull(CameraRecognizerManager.ActiveCamera);
        }
        void DnBugCull(Camera camera)
        {
            if (m_renderInfo == null)
            {
                return;
            }
            foreach (var rendererInfo in m_renderInfo)
            {
                var rendererInstanceInfo = rendererInfo.Value;
                var matrix4x4pool = rendererInstanceInfo.GetMatrix4x4();
                var poolCountcount = rendererInstanceInfo.GetPoolCount();
                var mesh = rendererInstanceInfo.GetMesh();
                for (int i = 0; i < poolCountcount; i++)
                {
                    var count = matrix4x4pool[i].length;
                    var matrixs = matrix4x4pool[i].OnePool;
                    for (int j = 0; j < count; j++)
                    {
                        Bounds localBounds = mesh.bounds;
                        Bounds worldBounds = BoundsUtils.CalcLocalBounds(localBounds, matrixs[j]);
                        Color _color = Color.gray;
                        if (m_spaceManager.CompletelyCull(worldBounds, out var insCompletely))
                        {
                            if (insCompletely)
                            {
                                _color = Color.black;
                            }
                            else
                            {
                                _color = Color.yellow;
                            }
                        }
                        else
                        {
                            _color = Color.red;
                        }
                        DrawGizmos(worldBounds, 1.0f, _color);
                    }
                }
            }
        }
        #endregion
#endif
        

    }
}