using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.CustomData;
using Unity.MergeInstancingSystem.New;
using Unity.MergeInstancingSystem.Utils;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.MergeInstancingSystem
{

   [BurstCompile]
    public unsafe struct TreeNodeUpdateShadowJob : IJob
    {
        [ReadOnly] 
        [NativeDisableUnsafePtrRestriction]
        public JobTreeData* treeNodes;

        [Collections.ReadOnly] 
        [NativeDisableUnsafePtrRestriction]
        public DPlane* planes;
        
        
        [NativeDisableUnsafePtrRestriction]
        public DElement* instanceElements;
        
        [ReadOnly] 
        public NativeArray<int> lodNumbers;
        
        public int root;
        public void Execute()
        {
            NativeQueue<int> spaceNodes = new NativeQueue<int>(Allocator.Temp);
            spaceNodes.Enqueue(root);
            while (spaceNodes.Count > 0)
            {
                int index = spaceNodes.Dequeue();
                ref JobTreeData node = ref treeNodes[index];
                ref var box = ref node.m_box;
                //根据距离剔除是否通过
                int visible = 1;
                float2 distRadius = new float2(0, 0);
                for (int planeIndex = 0; planeIndex < 6; ++planeIndex)
                {
                    ref DPlane plane = ref planes[planeIndex];
                    distRadius.x = math.dot(plane.normalDist.xyz, box.center) + plane.normalDist.w;
                    distRadius.y = math.dot(math.abs(plane.normalDist.xyz), box.extents);
                    visible = math.select(visible,0,  distRadius.x + distRadius.y < 0);
                }
                //视锥体剔除是否通过
                bool viewCull = visible == 1;
                if (viewCull)
                {
                    for (int i = node.objhead; i < node.objlength + node.objhead; i++)
                    {
                        GameObjCull(instanceElements,i,lodNumbers);
                    }
                }
                if (viewCull && node.hasChild)
                {
                    spaceNodes.Enqueue(node.child_0);
                    spaceNodes.Enqueue(node.child_1);
                    spaceNodes.Enqueue(node.child_2);
                    spaceNodes.Enqueue(node.child_3);
                }
            }
           
        }
        public void GameObjCull(DElement* instanceElements,int elementsIndex,NativeArray<int> lodNumbers)
        {
            int visible = 1;
            float2 distRadius = new float2(0, 0);
            ref DElement treeElement = ref instanceElements[elementsIndex];
            for (int planeIndex = 0; planeIndex < 6; ++planeIndex)
            {
                ref DPlane plane = ref planes[planeIndex];
                distRadius.x = math.dot(plane.normalDist.xyz, treeElement.m_bounds.center) + plane.normalDist.w;
                distRadius.y = math.dot(math.abs(plane.normalDist.xyz), treeElement.m_bounds.extents);
                visible = math.select(visible, 0, distRadius.x + distRadius.y < 0);
            }
            if (visible == 1)
            {
                int lodNumber = lodNumbers[treeElement.m_mark];
                treeElement.m_visible = true;
                treeElement.m_lodLevel = lodNumber - 1;
            }
        }
    }
   [BurstCompile]
    public unsafe struct ResetElementState : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction]
        public DElement* gameobject;
        public void Execute(int index)
        {
            ref DElement ele = ref gameobject[index];
            ele.m_visible = false;
            ele.m_lodLevel = 0;
        }
    }
    
    [BurstCompile]
    public unsafe struct TreeNodeUpadateJob : IJob
    {
        [ReadOnly] 
        [NativeDisableUnsafePtrRestriction]
        public JobTreeData* treeNodes;

        [Collections.ReadOnly] 
        [NativeDisableUnsafePtrRestriction]
        public DPlane* planes;
        
        
        [NativeDisableUnsafePtrRestriction]
        public DElement* instanceElements;
        
        [ReadOnly]
        public NativeArray<IntPtr> lodInfos;
        
        [ReadOnly] 
        public NativeArray<int> lodNumbers;
        
        public int root;
        public float3 cameraPos;
        public float4x4 matrix_Proj;
        
        public void Execute()
        {
            NativeQueue<int> spaceNodes = new NativeQueue<int>(Allocator.Temp);
            spaceNodes.Enqueue(root);
            while (spaceNodes.Count > 0)
            {
                int index = spaceNodes.Dequeue();
                ref JobTreeData node = ref treeNodes[index];
                ref var box = ref node.m_box;
                //根据距离剔除是否通过
                int visible = 1;
                float2 distRadius = new float2(0, 0);
                for (int planeIndex = 0; planeIndex < 6; ++planeIndex)
                {
                    ref DPlane plane = ref planes[planeIndex];
                    distRadius.x = math.dot(plane.normalDist.xyz, box.center) + plane.normalDist.w;
                    distRadius.y = math.dot(math.abs(plane.normalDist.xyz), box.extents);
                    visible = math.select(visible,0,  distRadius.x + distRadius.y < 0);
                }
                //视锥体剔除是否通过
                bool viewCull = visible == 1;
                if (viewCull)
                {
                    for (int i = node.objhead; i < node.objlength + node.objhead; i++)
                    {
                        GameObjCull(instanceElements,i,lodInfos,lodNumbers,cameraPos,matrix_Proj);
                    }
                }
                if (viewCull && node.hasChild)
                {
                    spaceNodes.Enqueue(node.child_0);
                    spaceNodes.Enqueue(node.child_1);
                    spaceNodes.Enqueue(node.child_2);
                    spaceNodes.Enqueue(node.child_3);
                }
            }
        }

        public void GameObjCull(DElement* instanceElements,int elementsIndex,NativeArray<IntPtr> lodInfos,NativeArray<int> lodNumbers,float3 viewOringin,float4x4 matrix_Proj)
        {
            int visible = 1;
            float2 distRadius = new float2(0, 0);
            ref DElement treeElement = ref instanceElements[elementsIndex];
            IntPtr addres = lodInfos[treeElement.m_mark];
            float* lodinfo = (float*)addres.ToPointer();
            for (int planeIndex = 0; planeIndex < 6; ++planeIndex)
            {
                ref DPlane plane = ref planes[planeIndex];
                distRadius.x = math.dot(plane.normalDist.xyz, treeElement.m_bounds.center) + plane.normalDist.w;
                distRadius.y = math.dot(math.abs(plane.normalDist.xyz), treeElement.m_bounds.extents);
                visible = math.select(visible, 0, distRadius.x + distRadius.y < 0);
            }
            if (visible == 1)
            {
                int lodNumber = lodNumbers[treeElement.m_mark];
                int lodLevel = ComputeLOD(lodNumber,viewOringin,matrix_Proj,ref treeElement.m_sphers,ref treeElement.m_bounds,lodinfo);
                treeElement.m_visible = true;
                treeElement.m_lodLevel = lodLevel;
            }
        }
        private int ComputeLOD(int numLOD,float3 viewOringin,float4x4 matrix_Proj,ref DSphere boundSphere,ref DAABB boundBox,float* lODInfos)
        {
            //球半径在屏幕上的投影
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
    }
    
    [BurstCompile]
    public unsafe struct InstanceScatterJob : IJobParallelFor
    {
        [ReadOnly] 
        [NativeDisableUnsafePtrRestriction]
        public DAABB* boxs;
        
        [ReadOnly] 
        [NativeDisableUnsafePtrRestriction]
        public Matrix4x4* matrix;
        
        [NativeDisableUnsafePtrRestriction]
        public DElement* treeElements;
        
        public void Execute(int index)
        {
            ref DElement element = ref treeElements[index];
            ref Matrix4x4 matrixWorld = ref matrix[element.m_dataIndex];
            ref DAABB boundBox = ref boxs[element.m_mark];
            element.m_bounds = Geometry.CaculateWorldBound(boundBox, matrixWorld);
            element.m_sphers = new DSphere(Geometry.CaculateBoundRadius(element.m_bounds), element.m_bounds.center);
        }
    }
    
    [BurstCompile]
    public struct DInstanceDataJob : IJobParallelFor
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
}
