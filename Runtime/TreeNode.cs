using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.Utils;
using UnityEngine;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public class TreeNode
    {
        [SerializeField]
        public DAABB m_Box;
        /// <summary>
        /// 树真正存放的地方
        /// </summary>
        [NonSerialized]
        public TreeNodeContainer m_container;
        /// <summary>
        /// 子树的索引
        /// </summary>
        [SerializeField]
        public List<int> m_childTreeNodeIds = new List<int>();
        /// <summary>
        /// 当前节点在整棵树中的层级
        /// </summary>
        [SerializeField]
        public int m_level;
        [SerializeField]
        public NodeGameObject m_Gameobj;
        [NonSerialized]
        public TreeNodeController m_controller;
        [SerializeField]
        public bool hasChild;

        [NonSerialized] 
        private NativeQueue<int> jobQueue;

        public void Initialize(TreeNodeController controller)
        {
            m_controller = controller;
            jobQueue = new NativeQueue<int>(Allocator.Persistent);
            for (int i = 0; i < m_childTreeNodeIds.Count; ++i)
            {
                var childTreeNode = m_container.Get(m_childTreeNodeIds[i]);
                childTreeNode.Initialize(controller);
            }
        }

        public unsafe void UpdateWithShadow(NativeList<JobHandle> taskJobHandles,int index,in DPlane* planes)
        {
            if (m_controller.m_useJob && m_level >= m_controller.m_jobBeginLevel)
            {
                TreeNodeUpdateShadowJob treeNodeUpdateShadowJob = new TreeNodeUpdateShadowJob();
                treeNodeUpdateShadowJob.planes = planes;
                treeNodeUpdateShadowJob.lodNumbers = m_controller.m_lodNumber;
                treeNodeUpdateShadowJob.treeNodes = (JobTreeData*)m_controller.m_jobGameJectData.GetUnsafePtr();
                treeNodeUpdateShadowJob.root = index;
                treeNodeUpdateShadowJob.instanceElements =  (DElement*)m_controller.m_instanceEle.GetUnsafePtr();
                treeNodeUpdateShadowJob.spaceNodes = jobQueue;
                taskJobHandles.Add(treeNodeUpdateShadowJob.Schedule());
                return;
            }
            int visible = 1;
            float2 distRadius = new float2(0, 0);
            for (int planeIndex = 0; planeIndex < 6; ++planeIndex)
            {
                ref DPlane plane = ref planes[planeIndex];
                distRadius.x = math.dot(plane.normalDist.xyz, m_Box.center) + plane.normalDist.w;
                distRadius.y = math.dot(math.abs(plane.normalDist.xyz), m_Box.extents);
                visible = math.select(visible,0,  distRadius.x + distRadius.y < 0);
            }
            //视锥体剔除是否通过
            bool viewCull = visible == 1;
            if (viewCull)
            {
                for (int i = m_Gameobj.head; i < m_Gameobj.number + m_Gameobj.head; i++)
                {
                    var lodNumber = m_controller.m_lodNumber[m_controller.m_instanceEle[i].m_mark];
                    var ele = m_controller.m_instanceEle[i];
                    ele.m_visible = true;
                    ele.m_lodLevel = lodNumber - 1;
                    m_controller.m_instanceEle[i] = ele;
                }
            }
            if (viewCull && hasChild)
            {
                for (int i = 0; i < m_childTreeNodeIds.Count; i++)
                {
                    int childIndex = m_childTreeNodeIds[i];
                    var node = m_container.Get(childIndex);
                    node.UpdateWithShadow(taskJobHandles, childIndex, planes);
                }
            }
        }
        /// <summary>
        /// 遍历树的地方
        /// </summary>
        /// <param name="taskJobHandles">如果启用多线程用这个来等线程同步</param>
        /// <param name="useJob">是否使用多线程</param>
        /// <param name="limitLevel">开始的Level</param>
        /// <param name="index">当前节点在树中的id</param>
        public unsafe void Update(NativeList<JobHandle> taskJobHandles,int index,in DPlane* planes,in float3 cameraPos,float4x4  matrixProj)
        {
            if (m_controller.m_useJob && m_level >= m_controller.m_jobBeginLevel)
            {
                TreeNodeUpadateJob upadataJob = new TreeNodeUpadateJob();
                upadataJob.planes = planes;
                upadataJob.cameraPos = cameraPos;
                upadataJob.root = index;
                upadataJob.treeNodes = (JobTreeData*)m_controller.m_jobGameJectData.GetUnsafePtr();
                upadataJob.matrix_Proj = matrixProj;
                upadataJob.lodNumbers = m_controller.m_lodNumber;
                upadataJob.instanceElements = (DElement*)m_controller.m_instanceEle.GetUnsafePtr();
                upadataJob.lodInfos = m_controller.m_lodPtr;
                upadataJob.spaceNodes = jobQueue;
                taskJobHandles.Add(upadataJob.Schedule());
                return;
            }
            bool viewCull = Geometry.IntersectAABBFrustum(m_Box, planes,
                out var completely);
            if (viewCull)
            {
                for (int i = m_Gameobj.head; i < m_Gameobj.number + m_Gameobj.head; i++)
                {
                    var lodNumber = m_controller.m_lodNumber[m_controller.m_instanceEle[i].m_mark];
                    var lodInfo = m_controller.m_lodInfos[m_controller.m_instanceEle[i].m_mark];
                    var spher = m_controller.m_instanceEle[i].m_sphers;
                    var box = m_controller.m_instanceEle[i].m_bounds;
                    var lodLevel = ComputeLOD(lodNumber,cameraPos,matrixProj,ref spher,ref box,lodInfo);
                    var ele = m_controller.m_instanceEle[i];
                    ele.m_visible = true;
                    ele.m_lodLevel = lodLevel;
                    m_controller.m_instanceEle[i] = ele;
                }
            }
            //通过剔除就遍历子
            if (viewCull && hasChild)
            {
                for (int i = 0; i < m_childTreeNodeIds.Count; i++)
                {
                    int childIndex = m_childTreeNodeIds[i];
                    var node = m_container.Get(childIndex);
                    node.Update(taskJobHandles, childIndex, planes, cameraPos, matrixProj);
                }
            }
        }
        private int ComputeLOD(int numLOD,float3 viewOringin,float4x4 matrix_Proj,ref DSphere boundSphere,ref DAABB boundBox,NativeArray<float> lODInfos)
        {
            float screenRadiusSqr = Geometry.ComputeBoundsScreenRadiusSquared(boundSphere.radius, boundBox.center, viewOringin, matrix_Proj);
            //Lod的总数
            for (int lodIndex = 0; lodIndex < numLOD; lodIndex++)
            {
                float treeLODInfo =  lODInfos[lodIndex];
                if (screenRadiusSqr >= MathExtent.sqr(treeLODInfo*0.5f))
                {
                    return lodIndex;
                }
            }
            return -1;
        }
        public void Dispose()
        {
            jobQueue.Dispose();
            for (int i = 0; i < m_childTreeNodeIds.Count; i++)
            {
                int childIndex = m_childTreeNodeIds[i];
                var node = m_container.Get(childIndex);
                node.Dispose();
            }
        }
        public void SetContainer(TreeNodeContainer container)
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
        public void SetChildTreeNode(TreeNode childNodes)
        {
            if (childNodes == null)
            {
                return;
            }
            int id = m_container.Add(childNodes);
            m_childTreeNodeIds.Add(id);
            childNodes.SetContainer(m_container);
        }
        public void SetChildTreeNode(List<TreeNode> childNodes)
        {
            if (childNodes == null)
            {
                return;
            }

            for (int i = 0; i < childNodes.Count; i++)
            {
                int id = m_container.Add(childNodes[i]);
                m_childTreeNodeIds.Add(id);
                childNodes[i].SetContainer(m_container);
            }
           
        }
    }
}