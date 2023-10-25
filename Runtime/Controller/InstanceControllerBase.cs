using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MergeInstancingSystem.Pool;
using Unity.MergeInstancingSystem.Render;
using Unity.MergeInstancingSystem.SpaceManager;
using Unity.MergeInstancingSystem.Utils;
using UnityEngine;
using UnityEditor;
using UnityEngine.Profiling;
using Matrix4x4 = System.Numerics.Matrix4x4;

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
        }
        
        public void Start()
        {
            m_root.Initialize(this, m_spaceManager, null);
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
            //
            // ------------------- 申请内存 ----------------------------------------
            try
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
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
           
        }
        public void OnDisable()
        {
            m_renderInfo.Clear();
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

        private ISpaceManager m_spaceManager;
        
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
        public void RecordInstance(List<NodeData> rendererObj,float dis,bool cullOBJ)
        {
            try
            {
                foreach (var nodeData in rendererObj)
                {
                    var mesh = m_instanceData.m_meshs[nodeData.m_meshIndex];
                    var mat = m_instanceData.m_materials[nodeData.m_material];
                    long identifier = nodeData.m_identifier;
                    if (m_renderInfo.TryGetValue(identifier,out var rendererInfo))
                    {
                        rendererInfo.m_CameraDis = dis;
                        AddDataToPool(nodeData,rendererInfo,cullOBJ);
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
                        tempInfo.m_CameraDis = dis;
                        AddDataToPool(nodeData, tempInfo,cullOBJ);
                        m_renderInfo.Add(identifier,tempInfo);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
           
        }
        
        
        /// <summary>
        /// 向事先申请好的内存中添加要渲染的数据
        /// </summary>
        /// <param name="nodeData">节点保存的数据信息</param>
        /// <param name="rendererInfo"></param>
        /// <param name="cullOBJ">是否挨个剔除节点中的OBJ</param>
        private void AddDataToPool(NodeData nodeData,RendererInfo rendererInfo,bool cullOBJ)
        {
            Profiler.BeginSample("Begin AddDataToPool");
            var poolId = rendererInfo.m_poolID;
            var Matrix4x4Source = m_instanceData.m_Matrix4X4sData;
            var lightMapIndexSource = m_instanceData.m_LightMapIndexsData;
            var lightMapScaleOffsetSource = m_instanceData.m_LightMapOffsetsData;
            //var LightProbesSource = m_instanceData.m_LightProbes;
            var renderDataList = nodeData.m_RenderData;
            //这里之前有GC
            var planes = CameraRecognizerManager.ActiveRecognizer.planes;
            foreach (var listInfo in renderDataList)
            {
                if (cullOBJ)
                {
                    var localBounds = rendererInfo.m_mesh.bounds;
                    for (int i = 0; i < listInfo.length; i++)
                    {
                        int index = listInfo.head + i;
                        var localToWorld = m_instanceData.m_Matrix4X4sData[index];
                        var worldBounds = TransformBoundsToWorldBounds(localToWorld, localBounds);
                        if (GeometryUtility.TestPlanesAABB(planes, worldBounds))
                        {
                            rendererInfo.m_renderCount += 1;
                            PoolManager.Instance.CopyData(poolId.m_matrix4x4ID,m_instanceData.m_Matrix4X4sData,listInfo.head + i,1);
                            if (nodeData.m_NeedLightMap)
                            {
                                PoolManager.Instance.CopyData(poolId.m_lightMapIndexId,m_instanceData.m_LightMapIndexsData,listInfo.head + i,1);
                                PoolManager.Instance.CopyData(poolId.m_lightMapScaleOffsetID,m_instanceData.m_LightMapOffsetsData,listInfo.head + i,1);
                            }
                        }
                    }
                }
                else
                {
                    int head = listInfo.head;
                    int length = listInfo.length;
                    rendererInfo.m_renderCount += length;
                    PoolManager.Instance.CopyData(poolId.m_matrix4x4ID,Matrix4x4Source,head,length);
                    if (nodeData.m_NeedLightMap && head >=0)
                    {
                        PoolManager.Instance.CopyData(poolId.m_lightMapIndexId,lightMapIndexSource,head,length);
                        PoolManager.Instance.CopyData(poolId.m_lightMapScaleOffsetID,lightMapScaleOffsetSource,head,length);
                   
                    }
                }
            }
            Profiler.EndSample();
        }
        //----------- 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="matrix"></param>
        /// <param name="localBounds"></param>
        /// <returns></returns>
        Bounds TransformBoundsToWorldBounds(UnityEngine.Matrix4x4 matrix, Bounds localBounds)
        {
            var position = matrix.GetPosition();
            Vector3 worldAABBMin = localBounds.min + position;
            Vector3 worldAABBMax = localBounds.max + position;
            Vector3 center = (worldAABBMin + worldAABBMax) * 0.5f;
            Vector3 size = worldAABBMax - worldAABBMin;
            Bounds worldBounds = new Bounds(center,size);
            return worldBounds;
        }
        /// <summary>
        /// 更新四叉树
        /// </summary>
        /// <param name="camera"></param>
        public void UpdateCull(Camera camera)
        {
            Profiler.BeginSample("Begin Updata Tree");
            if (m_spaceManager == null)
                return;
            m_spaceManager.UpdateCamera(this.transform, camera);
            m_root.Update(m_lodDistance,usePreciseCulling);
            //将一课树需要渲染的注册一下
            RegsiterRender();
            Profiler.EndSample();
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
                if (info.CastShadow)
                {
                    InstanceManager.Instance.RegisterShadowRenderlist(info);
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
        }


        public void Dispose()
        {
            foreach (var rendererInfo in m_renderInfo)
            {
                rendererInfo.Value.Dispose();
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
            foreach (var nodeData in node.HighObjectIds)
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
            foreach (var nodeData in node.LowObjectIds)
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
                        var matrix4X4 = matrixs[j];
                        Bounds localBounds = new Bounds();
                        if (mesh.bounds != null)
                        {
                            localBounds = mesh.bounds;
                        }
                        else
                        {
                            Debug.Log(mesh.name);
                        }

                        Bounds worldBounds = TransformBoundsToWorldBounds(matrix4X4, localBounds);
                        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
                        Color _color = Color.gray;
                        if (GeometryUtility.TestPlanesAABB(planes, worldBounds))
                        {
                            _color = Color.yellow;
                        }
                        else
                        {
                            _color = Color.red;
                        }

                        DrawGizmos(worldBounds, 1.0f,_color);
                    }
                }
            }
        }
        #endregion
#endif
        

    }
}