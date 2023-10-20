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
            m_positionPoolMap = new Dictionary<int, PoolID>();
            m_renderInfo = new Dictionary<int, RendererInfo>();
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
            // else
            // {
            //     ID.m_lightProbeID = PoolManager.Instance.AllocatLightProbePool(count);
            // }
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
                var matAndMeshIdentifiers = m_instanceData.m_matAndMeshIdentifiers;
                // m_identifierCounts :上面的标识符表示的这种类型有多少个实例需要渲染
                var identifierCounts = m_instanceData.m_identifierCounts;
                // m_identifierUseLightMapOrProbe ：这种类型需要LightMap吗
                var useLightMap = m_instanceData.m_identifierUseLightMapOrProbe;
                for (int i = 0; i < matAndMeshIdentifiers.Count; i++)
                {
                    //Count 是该批次有多少个OBJ
                    var count = identifierCounts[i];
                    var identifier = matAndMeshIdentifiers[i];
                    bool useLightMapOrProbe = useLightMap[i];
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
        private Dictionary<int, PoolID> m_positionPoolMap;
        
        /// <summary>
        /// 每种渲染类型对应的渲染数据,渲染类型指的是Mesh+Mat的唯一组合,需要每帧清理
        /// </summary>
        private Dictionary<int, RendererInfo> m_renderInfo;

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
                    int identifier = nodeData.m_identifier;
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
            var poolId = rendererInfo.m_poolID;
            var Matrix4x4Source = m_instanceData.m_localToWorlds;
            var lightMapIndexSource = m_instanceData.m_lightMapIndex;
            var lightMapScaleOffsetSource = m_instanceData.m_lightMapScaleOffset;
            //var LightProbesSource = m_instanceData.m_LightProbes;
            var matrix4x4data = nodeData.m_matrix4x4Data;
            var giData = nodeData.m_GIData;
            foreach (var listInfo in matrix4x4data)
            {
                int head = listInfo.head;
                int length = listInfo.length;
                rendererInfo.m_renderCount += length;
                PoolManager.Instance.CopyData(poolId.m_matrix4x4ID,Matrix4x4Source,head,length);
            }
            //分开拷贝矩阵数据和GI数据
            foreach (var gInfo in giData)
            {
                int head = gInfo.head;
                int length = gInfo.length;
                bool useLightMap = nodeData.m_NeedLightMap;
                if (useLightMap && head >=0)
                {
                    PoolManager.Instance.CopyData(poolId.m_lightMapIndexId,lightMapIndexSource,head,length);
                    PoolManager.Instance.CopyData(poolId.m_lightMapScaleOffsetID,lightMapScaleOffsetSource,head,length);
                   
                }
            }
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
                for (int i = 0; i < nodeData.m_matrix4x4Data.Count; i++)
                {
                    meshCount += nodeData.m_matrix4x4Data[i].length;
                }
                var mesh = m_instanceData.m_meshs[meshIndex];
                HighVSCount += mesh.vertexCount * meshCount;
            }
            foreach (var nodeData in node.LowObjectIds)
            {
                int meshIndex = nodeData.m_meshIndex;
                int meshCount = 0;
                for (int i = 0; i < nodeData.m_matrix4x4Data.Count; i++)
                {
                    meshCount += nodeData.m_matrix4x4Data[i].length;
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
        Bounds TransformBoundsToWorldBounds(UnityEngine.Matrix4x4 matrix, Bounds localBounds)
        {
            Vector3 center = matrix.MultiplyPoint(localBounds.center);
            Vector3 size = matrix.MultiplyVector(localBounds.size);

            return new Bounds(center, size);
        }
        #endregion
#endif
        

    }
}