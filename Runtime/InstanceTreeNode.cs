using System;
using System.Collections.Generic;
using Unity.MergeInstancingSystem.SpaceManager;
using UnityEngine;
using Unity.MergeInstancingSystem.Controller;
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
        [SerializeField] private Bounds m_bounds;

        [SerializeField] private Bounds m_CullBounds;

        [NonSerialized] private InstanceTreeNodeContainer m_container;


        [SerializeField] private List<int> m_childTreeNodeIds = new List<int>();
        
        
        /// <summary>
        /// 自己包围盒围住的OBJ
        /// </summary>

        [SerializeField] private List<NodeData> m_highObjectIds = new List<NodeData>();

        /// <summary>
        /// 子节点的OBJ
        /// </summary>

        [SerializeField] private List<NodeData> m_lowObjectIds = new List<NodeData>();
        
        
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
        public Bounds Bounds
        {
            set { m_bounds = value; }
            get { return m_bounds; }
        }
        
        public Bounds CullBounds
        {
            set { m_CullBounds = value; }
            get { return m_CullBounds; }
        }
        public List<NodeData> LowObjectIds
        {
            set
            {
                m_lowObjectIds = value;
            }
            get { return m_lowObjectIds; }
        }
        public List<NodeData> HighObjectIds
        {
            set
            {
                m_highObjectIds = value;
            }
            get { return m_highObjectIds; }
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
        /// <summary>
        /// 一开始的时候每个节点的可见性都为True
        /// </summary>
        private bool m_isVisible;
        private bool m_isVisibleHierarchy;

        public void Initialize(InstanceControllerBase controller, ISpaceManager spaceManager, InstanceTreeNode parent)
        {
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.Initialize(controller, spaceManager, this);
            }

            m_expectedState = State.Release;
            m_controller = controller;
            m_spaceManager = spaceManager;
            m_boundsLength = m_bounds.extents.x * m_bounds.extents.x + m_bounds.extents.z * m_bounds.extents.z;
            foreach (var VARIABLE in m_highObjectIds)
            {
                VARIABLE.CreatePropretyBlock();
            }

            foreach (var VARIABLE in m_lowObjectIds)
            {
                VARIABLE.CreatePropretyBlock();
            }
        }
        public void Cull(bool isCull)
        {
            if (isCull)
            {
                return;
            }
        }
        public void Update(float lodDistance,bool enableOreciseCulling)
        {
            m_distance = m_spaceManager.GetDistanceSqure(m_bounds)- m_boundsLength ;
            bool cullObj = false;
            // m_distance > 0 说明相机在当前节点的包围盒外面,<0就在里面不用判断剔除了
            if (m_distance > 0)
            {
                if ((m_spaceManager.IsCull(m_controller.CullDistance, m_bounds)))
                {
                    return;
                }
                m_expectedState = m_spaceManager.IsHigh(lodDistance, m_bounds) ? State.High : State.Low;
                if (m_expectedState == State.Low && ChildHasCull())
                {
                    m_expectedState = State.High;
                }
            }
            else
            {
                m_expectedState = State.High;
            }
            if (enableOreciseCulling && m_expectedState == State.High)
            {
                cullObj = m_spaceManager.IsBOXInsideViewFrustum(m_bounds);
            }
            //如果父物体为release 或者 Low 自己肯定不会被显示
            if (m_parent != null && (m_parent.ExprectedState == State.Release || m_parent.ExprectedState == State.Low))
            {
                m_expectedState = State.Release;
            }
            if (m_expectedState == State.High)
            {
                if (!m_spaceManager.IsCull(m_controller.CullDistance, m_CullBounds))
                {
                    m_controller.RecordInstance(HighObjectIds,m_distance,cullObj);
                }
                
                
                for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
                {
                    var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                    childTreeNode.Update(lodDistance,enableOreciseCulling);
                }
            }
            else if (m_expectedState == State.Low)
            {
                m_controller.RecordInstance(LowObjectIds,m_distance,cullObj);
                UpDataNodeState();
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
        private void UpDataNodeState()
        {
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.ExprectedState = State.Release;
                childTreeNode.UpDataNodeState();
            }
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
        
    }
}