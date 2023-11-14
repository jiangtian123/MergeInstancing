using System;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.CustomData;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.MergeInstancingSystem.Job
{

    public struct SmallTreeNode
    {
        public DAABB m_highBox;
        public DAABB m_lowBox;
        public int child_0;
        public int child_1;
        public int child_2;
        public int child_3;
        public bool hasChild;
        public SmallTreeNode(DAABB highBox, DAABB lowBox, List<int> child)
        {
            m_highBox = highBox;
            m_lowBox = lowBox;
            if (child.Count == 0)
            {
                hasChild = false;
                child_0 = -1;
                child_1 = -1;
                child_2 = -1;
                child_3 = -1;
            }
            else
            {
                hasChild = true;
                child_0 = child[0];
                child_1 = child[1];
                child_2 = child[2];
                child_3 = child[3];
            }
        }
    }
    
    public unsafe struct DInstanceDataJob : IJobParallelFor
    {
        [Collections.ReadOnly] 
        public NativeArray<DTransform> transforms;
        [WriteOnly] 
        public NativeArray<Matrix4x4> matrix_Worlds;

        public void Execute(int index)
        {
            Matrix4x4 matrixWorld = float4x4.TRS(transforms[index].position,
                transforms[index].rotation, transforms[index].scale);
            matrix_Worlds[index] = matrixWorld;
        }
    }
    /// <summary>
    /// 求一个节点内的HighObj的包围盒
    /// </summary>
    public unsafe struct DInitNodeDataBox : IJob
    {
        [Collections.ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public Matrix4x4* localToWords;
        
        public DAABB meshAABB;
        
        [Collections.ReadOnly]
        public NativeList<NodeData.ListInfo> dataIndex;

        [WriteOnly] 
        public NativeArray<DAABB> highBoxs;
        public void Execute()
        {
            for (int i = 0; i < dataIndex.Length; i++)
            {
                for (int j = 0; j < dataIndex[i].length; j++)
                {
                    int index = dataIndex[i].head + j;
                    ref Matrix4x4 localToword = ref localToWords[index];
                    highBoxs[i + j] = Geometry.CaculateWorldBound(meshAABB, localToword);
                }
            }
        }
    }
    public unsafe struct TreeCullingJob : IJob
    {
        [Collections.ReadOnly]
        public NativeArray<NodeData.ListInfo> dataIndex;
        
        [Collections.ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public DAABB* objectBounds;
        
        
        [Collections.ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public DPlane* planes;
        
        [WriteOnly]
        public NativeList<NodeData.ListInfo> result;
        public void Execute()
        {
            for (int i = 0; i < dataIndex.Length; i++)
            {
                var listInfo = dataIndex[i];
                int head = listInfo.head;
                int length = 0;
                for (int j = 0; j < listInfo.length; j++)
                {
                    float2 distRadius = new float2(0, 0);
                    int index = i + j;
                    var box = objectBounds[index];
                    bool isPass = true;
                    for (int PlaneIndex = 0; PlaneIndex < 6; ++PlaneIndex)
                    {
                        ref DPlane plane = ref planes[PlaneIndex];
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
                    if((j+1 == listInfo.length || !isPass)&&length!=0)
                    {
                        NodeData.ListInfo temp = new NodeData.ListInfo();
                        temp.head = head;
                        temp.length = length;
                        result.Add(temp);
                        head += (length + 1);
                        length = 0;
                    }
                }
            }
        }
        
    }
    public unsafe struct TreeNodeUpdate : IJob
    {
        [Collections.ReadOnly] 
        [NativeDisableUnsafePtrRestriction]
        public SmallTreeNode* _treeNode;

        public int root;
        public float3 cameraPosition;
        public float lodDis;
        public float preRelative;
        public float cullDis;
        [Collections.ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public DPlane* planes;
        
        [WriteOnly]
        [NativeDisableUnsafePtrRestriction]
        public byte* result;
        public void Execute()
        {
            NativeQueue<int> spaceNodes = new NativeQueue<int>(Allocator.Temp);
            spaceNodes.Enqueue(root);
            while (spaceNodes.Count > 0)
            {
                int index = spaceNodes.Dequeue();
                ref SmallTreeNode node = ref _treeNode[index];
                byte state =  NodeState(ref node,cameraPosition,ref planes,cullDis,lodDis,preRelative);
                //只有这个节点没被剔除并且为Hig才判断子节点。
                if (node.hasChild && (state & 1)!=0 && ((state >>2)& 1)== 1)
                {
                    var cullState = IsCull(ref node.m_highBox,ref planes);
                    //将state的后两位替换掉
                    state = (byte)((state&0xfc)|cullState);
                    spaceNodes.Enqueue(node.child_0);
                    spaceNodes.Enqueue(node.child_1);
                    spaceNodes.Enqueue(node.child_2);
                    spaceNodes.Enqueue(node.child_3);
                }
                result[index +1] = state;
            }
        }
        /// <summary>
        /// 返回是否需要精确剔除和是High或者Low
        /// </summary>
        /// <param name="node"></param>
        /// <param name="cameraPos"></param>
        /// <param name="planes"></param>
        /// <param name="cullDis"></param>
        /// <param name="lodDis"></param>
        /// <param name="preRelative">一个Byte，前三位分别代表 是否被剔除，是否需要精确剔除（），是Hig还是Low（1是high）</param>
        /// <returns></returns>
        byte NodeState(ref SmallTreeNode node,float3 cameraPos,ref DPlane* planes,float cullDis,float lodDis,float preRelative)
        {
            ref DAABB sectorBound = ref node.m_lowBox;
            //根据距离剔除
            bool disCull = Geometry.DisCull(cameraPos, ref sectorBound, preRelative, cullDis);
            if (disCull)
            {
                return (byte)(0x0);
            }
            //做视锥体剔除返回是否剔除和是否需要精确剔除
            var cullState = IsCull(ref sectorBound,ref planes);
            //判断High Or Low
            int highOrLow = 0x0;
            if ((cullState&0x1) != 0)
            {
                highOrLow = Geometry.IsHigh(lodDis, sectorBound, preRelative, cameraPos) ? 1 : 0;
            }

            int result = highOrLow << 2 | cullState;
            return (byte)result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="box"></param>
        /// <param name="planes"></param>
        /// <returns>第一位是是否剔除（1是不剔除），第二位是是否精确剔除（1是需要精确剔除）</returns>
        byte IsCull(ref DAABB box, ref DPlane* planes)
        {
            ref DAABB sectorBound = ref box;
            int isCompletely = 0;
            int visible = 0;
            NativeArray<float3> points = new NativeArray<float3>(8, Allocator.Temp);
            float3 min = sectorBound.min;
            float3 max = sectorBound.max;
            if ((min.x == max.x) && (min.y == max.y) && (min.z == max.z))
            {
                return (byte)visible;
            }
            points[0] = new float3(min.x, min.y, min.z);
            points[1] = new float3(min.x, min.y, max.z);
            points[2] = new float3(max.x, min.y, max.z);
            points[3] = new float3(max.x, min.y, min.z);
            
            points[4] = new float3(min.x, max.y, min.z);
            points[5] = new float3(min.x, max.y, max.z);
            points[6] = new float3(max.x, max.y, max.z);
            points[7] = new float3(max.x, max.y, min.z);
            for(int p = 0; p < 6; ++p)
            {
                bool inside = false;
                for(int c = 0; c < 8; ++c)
                {
                    //用包围盒8个点判断
                    //只要有一个点在这个面里面，就不判断了
                    if(planes[p].GetSide(points[c]))
                    {
                        inside = true;
                        break;
                    }
                    isCompletely = 1;
                }
                //所有顶点都在包围盒外，被剔除。
                if(!inside)
                {
                    isCompletely = 1;
                    visible = 0;
                    break;
                }
                visible = 1;
            }
            points.Dispose();
            return (byte)(isCompletely << 1 | visible);
        }
        
    }
    public struct ResetStateJob : IJobParallelFor
    {
        [WriteOnly] 
        public NativeArray<byte> restArray;

        public void Execute(int index)
        {
            restArray[index] = 0;
        }
    }
    public unsafe struct ShaodwCullJob : IJob
    { 
        [Collections.ReadOnly]
        public NativeArray<NodeData.ListInfo> dataIndex;
        [Collections.ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public DAABB* objectBounds;
        
        public float3 cameraPos;
        public float cullDis;
        [WriteOnly]
        public NativeList<NodeData.ListInfo> result;
        public void Execute()
        {
            for (int i = 0; i < dataIndex.Length; i++)
            {
                var listInfo = dataIndex[i];
                int head = listInfo.head;
                int length = 0;
                for (int j = 0; j < listInfo.length; j++)
                {
                    ref var box = ref objectBounds[i+j];
                    float dis = Geometry.GetDistance(cameraPos, box.center);
                    float size = Geometry.GetBoxLength(ref box);
                    bool vis = dis - size < cullDis;
                    if (vis)
                    {
                        length += 1;
                    }
                    else if (length == 0)
                    {
                        head++; 
                    }
                    if((j+1 == listInfo.length || !vis)&&length!=0)
                    {
                        NodeData.ListInfo temp = new NodeData.ListInfo();
                        temp.head = head;
                        temp.length = length;
                        result.Add(temp);
                        head += (length + 1);
                        length = 0;
                    }
                }
            }
        }
    }
    
}