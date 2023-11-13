using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.MergeInstancingSystem.SpaceManager;
using UnityEngine;
using Unity.MergeInstancingSystem.Controller;
using Unity.MergeInstancingSystem.CustomData;
using Unity.MergeInstancingSystem.Job;
using Unity.MergeInstancingSystem.Utils;
using UnityEngine.Profiling;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public class InstanceTreeNode
    {
        /// <summary>
        /// 当前节点在树中的哪一层
        /// </summary>
        [SerializeField] private int m_level;
        
        /// <summary>
        /// 当前节点的包围盒
        /// </summary>
        [SerializeField] private DAABB m_bounds;

        [SerializeField] private DAABB m_HighCullBounds;
        
        [SerializeField] private DAABB m_LowCullBounds;

        [NonSerialized] private InstanceTreeNodeContainer m_container;


        [SerializeField] public List<int> m_childTreeNodeIds = new List<int>();
        [SerializeField] private int childCount;
        /// <summary>
        /// 自己包围盒围住的OBJ
        /// </summary>

        [SerializeField] private List<NodeData> m_highObjects = new List<NodeData>();

        /// <summary>
        /// 子节点的OBJ
        /// </summary>

        [SerializeField] private List<NodeData> m_lowObjects = new List<NodeData>();
        
        
        
        UnityEngine.Profiling.CustomSampler UpdateSampler;
        public enum State
        {
            Release,
            Low,
            High,
        }
        
        public int Level
        {
            set { m_level = value; }
            get { return m_level; }
        }
        public DAABB Bounds
        {
            set { m_bounds = value; }
            get { return m_bounds; }
        }
        
        public DAABB HighCullBounds
        {
            set { m_HighCullBounds = value; }
            get { return m_HighCullBounds; }
        }
        public DAABB LowCullBounds
        {
            set { m_LowCullBounds = value; }
            get { return m_LowCullBounds; }
        }

        public int ChildNumber
        {
            get
            {
                return childCount;
            }
            set
            {
                childCount = value;
            }
        }
        public List<NodeData> LowObjects
        {
            set
            {
                m_lowObjects = value;
            }
            get { return m_lowObjects; }
        }
        public List<NodeData> HighObjects
        {
            set
            {
                m_highObjects = value;
            }
            get { return m_highObjects; }
        }
        public State ExprectedState
        {
            get { return m_expectedState; }
            set
            {
                m_expectedState = value;
            }
        }
        public InstanceControllerBase Controller
        {
            get
            {
                return m_controller;
            }
        }
        private State m_expectedState = State.Release;
        private InstanceTreeNode m_parent;
        private InstanceControllerBase m_controller;
        
        private ISpaceManager m_spaceManager;
        private float m_boundsLength;
        private float m_distance;
        private NativeList<JobHandle> nodetaskHandles;
        public void Initialize(InstanceControllerBase controller, ISpaceManager spaceManager, InstanceTreeNode parent)
        {
            nodetaskHandles = new NativeList<JobHandle>(Allocator.Persistent);
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.Initialize(controller, spaceManager, this);
            }
            m_expectedState = State.Release;
            m_controller = controller;
            m_spaceManager = spaceManager;
            m_boundsLength = m_bounds.extents.x * m_bounds.extents.x + m_bounds.extents.z * m_bounds.extents.z;
            UpdateSampler = UnityEngine.Profiling.CustomSampler.Create("UpDate Tree Node");
            var taskHandles = new NativeList<JobHandle>(256, Allocator.Temp);
            foreach (var VARIABLE in m_highObjects)
            {
                VARIABLE.Init(m_controller.InstanceData,taskHandles);
                VARIABLE.CreatePropretyBlock();
            }
            foreach (var VARIABLE in m_lowObjects)
            {
                VARIABLE.Init(m_controller.InstanceData,taskHandles);
                VARIABLE.CreatePropretyBlock();
            }
            
            JobHandle.CompleteAll(taskHandles);
            taskHandles.Dispose();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="lodDistance"></param>
        public void Update(float lodDistance)
        {
            m_expectedState = State.High;
            if (m_spaceManager.IsCull(m_controller.CullDistance,m_bounds))
            {
                    m_expectedState = State.Release;
                    //UpdateSampler.End();
                    return;
            }
            m_expectedState = m_spaceManager.IsHigh(lodDistance, m_bounds) ? State.High : State.Low;
            bool cullObj = false;
            //如果被剔除就出去
            if (m_expectedState == State.Low)
            {
                //如果用Low的包围盒做剔除判断
                if (!m_spaceManager.CompletelyCull(m_LowCullBounds, out cullObj))
                {
                    m_expectedState = State.Release;
                    //UpdateSampler.End();
                    return;
                }
                //如果有一部分在外面，则退化成High，去判断子
                if(cullObj && m_controller.usePreciseCulling)
                {
                    m_expectedState = State.High;
                }
            }
            //如果当前状态为High
            if (m_expectedState == State.High)
            {
                //没有通过视锥剔除
                if (!m_spaceManager.CompletelyCull(m_HighCullBounds, out cullObj))
                {
                    m_expectedState = State.Release;
                }
                else
                {
                    m_controller.RecordInstance(HighObjects);
                }
                //对子节点进行处理
                for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
                {
                    var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                    childTreeNode.Update(lodDistance);
                }
            }
            else
            {
                //如果状态为Low就直接提交
                m_controller.RecordInstance(m_lowObjects); 
            }
            //UpdateSampler.End();
        }

        public unsafe void UpdateWithJob(NativeList<JobHandle> tasJobHandles,bool useJob,int litmitLevel,int index)
        {
            if (useJob && Level >= litmitLevel)
            {
                TreeNodeUpdate jobUpdate = new TreeNodeUpdate();
                {
                    jobUpdate.planes = (DPlane*)CameraRecognizerManager.ActiveRecognizer.planes.GetUnsafePtr();
                    jobUpdate.root = index - 1;
                    jobUpdate._treeNode = (SmallTreeNode*)m_controller.smallTreeNodes.GetUnsafePtr();
                    jobUpdate.lodDis = m_controller.LODDistance;
                    jobUpdate.cameraPosition = CameraRecognizerManager.ActiveRecognizer.cameraPos;
                    jobUpdate.cullDis = m_controller.CullDistance;
                    jobUpdate.preRelative = CameraRecognizerManager.ActiveRecognizer.preRelative;
                    jobUpdate.result = (byte*)m_controller.nodesState.GetUnsafePtr();
                }
                tasJobHandles.Add(jobUpdate.Schedule());
                return;
            }
            //根据距离剔除
            if (m_spaceManager.IsCull(m_controller.CullDistance,m_bounds))
            {
                int state = 0x1 << 2 | 0x0 << 1 | 0x0;
                m_controller.nodesState[index] = (byte)state;
                m_expectedState = State.Release;
                return;
            }
            bool cullObj = false;
            int cullState = 0;
            //视锥剔除没过
            if (!m_spaceManager.CompletelyCull(m_LowCullBounds, out cullObj))
            {
                m_expectedState = State.Release;
                int state = 0x1 << 2 | 0x0 << 1 | 0x0;
                m_controller.nodesState[index] = (byte)state;
                return;
            }
            //判断状态
            m_expectedState = m_spaceManager.IsHigh(m_controller.LODDistance, m_bounds) ? State.High : State.Low;
            if (m_expectedState == State.High)
            {
                bool cull = m_spaceManager.CompletelyCull(m_HighCullBounds, out cullObj);
                int state = 0x1 << 2 | (cullObj ? 1 : 0) << 1 | (cull ? 0x1 : 0x0);
                m_controller.nodesState[index] = (byte)state;
                //是High 遍历子
                for (int i = 0; i < m_childTreeNodeIds.Count; i++)
                {

                    var node = m_container.Get(m_childTreeNodeIds[i]);
                    //将子节点的Index +1 是因为根节点也要写
                    node.UpdateWithJob(tasJobHandles, useJob, litmitLevel, m_childTreeNodeIds[i] + 1);
                }
            }
            else
            {
                int state = 0x0 << 2 | (cullObj? 1:0) << 1 | 0x1;
                m_controller.nodesState[index] = (byte)state;
            }
        }

        public void RegsiterRender(int index,NativeArray<byte> nodeState,bool usePreciseCulling)
        {
            var state = nodeState[index];
            bool isNeedSubmit =  (state & 0x1) == 1;
            bool isHigh = ((state >> 2) & 0x1) == 1;
            bool isNeedMoreCull = ((state >> 1) & 0x1) == 1;
            m_expectedState = State.Release;
            nodetaskHandles.Clear();
            //先判断状态
            if (isHigh)
            {
                //再判断是否需要剔除
                if (isNeedSubmit)
                {
                    m_expectedState = State.High;
                    if (isNeedMoreCull && usePreciseCulling)
                    {
                        foreach (var nodeData in m_highObjects)
                        {
                            nodeData.InitView(nodetaskHandles,CameraRecognizerManager.ActiveRecognizer.planes);
                        }
                    }
                    JobHandle.CompleteAll(nodetaskHandles);
                    m_controller.RecordInstance(m_highObjects); 
                }

                for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
                {
                    int childIndex = m_childTreeNodeIds[i];
                    var childTreeNode = m_container.Get(childIndex);
                    childTreeNode.RegsiterRender(childIndex + 1, nodeState,usePreciseCulling);
                }
            }
            else
            {
                if (isNeedSubmit)
                {
                    if (isNeedMoreCull && usePreciseCulling)
                    {
                        foreach (var nodeData in m_highObjects)
                        {
                            nodeData.InitView(nodetaskHandles,CameraRecognizerManager.ActiveRecognizer.planes);
                        }
                    }
                    JobHandle.CompleteAll(nodetaskHandles);
                    m_expectedState = State.Low;
                    m_controller.RecordInstance(m_lowObjects); 
                }
            }
        }
        public void Dispose()
        {
            nodetaskHandles.Dispose();
            foreach (var nodeData in m_highObjects)
            {
                nodeData.Dispose();
            }
            foreach (var nodeData in m_lowObjects)
            {
                nodeData.Dispose();
            }
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.Dispose();
            }
        }
        public void DispatchSetup(in NativeList<JobHandle> taskHandles)
        {
            if (m_expectedState == State.High)
            {
                foreach (var nodeData in m_highObjects)
                {
                    nodeData.CopyData(m_controller.InstanceData,taskHandles);
                }
            }
            else
            {
                foreach (var nodeData in m_lowObjects)
                {
                    nodeData.CopyData(m_controller.InstanceData,taskHandles);
                }
            }
        }
        
        private bool ChildHasCull()
        {
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                if (m_spaceManager.IsCull(m_controller.CullDistance, childTreeNode.m_bounds))
                {
                    return true;
                }
            }

            return false;
        }
        public void SetContainer(InstanceTreeNodeContainer container)
        {
            m_container = container;
            
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.SetContainer(container);
            }
        }

        public void Submit()
        {
            if (m_expectedState == State.High)
            {
                m_controller.RecordInstance(m_highObjects);
            }
            else if (m_expectedState == State.Low)
            {
                m_controller.RecordInstance(m_lowObjects);
            }
        }
        public int GetChildTreeNodeCount()
        {
            return m_childTreeNodeIds.Count;
        }
        
        public void SetChildTreeNode(List<InstanceTreeNode> childNodes)
        {
            ClearChildTreeNode();
            m_childTreeNodeIds.Capacity = childNodes.Count;

            for (int i = 0; i < childNodes.Count; ++i)
            {
                int id = m_container.Add(childNodes[i]);
                m_childTreeNodeIds.Add(id);
                childNodes[i].SetContainer(m_container);
            }
        }
        public void SetChildTreeNode(InstanceTreeNode childNodes)
        {
            if (childNodes == null)
            {
                return;
            }
            int id = m_container.Add(childNodes);
            m_childTreeNodeIds.Add(id);
            childNodes.SetContainer(m_container);
        }
        public void ClearChildTreeNode()
        {
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                m_container.Remove(m_childTreeNodeIds[i]);
            }
            m_childTreeNodeIds.Clear();
        }
        public InstanceTreeNode GetChildTreeNode(int index)
        {
            int id = m_childTreeNodeIds[index];
            return m_container.Get(id);
        }
        public int GetChildTreeNodeID(int index)
        {
            int id = m_childTreeNodeIds[index];
            return id;
        }
    }
}