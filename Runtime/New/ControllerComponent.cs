﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.CustomData;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.New
{
    public unsafe abstract class ControllerComponent : MonoBehaviour
    {
        internal static List<ControllerComponent> instanceComponents = new List<ControllerComponent>(8);
        void OnEnable()
        {
            instanceComponents.Add(this);
            OnRegiste();
        }
        void OnDisable()
        {
            instanceComponents.Remove(this);
            UnRegiste();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void OnRegiste();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void UnRegiste();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void UpDateTreeWithShadow(float3 cameraPos,float maxshadowDis,in NativeList<JobHandle> taskHandles);
        
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void UpDateTree(in DPlane* planes, in float3 cameraPos, in float4x4 matrixProj,in float preRelative, in NativeList<JobHandle> taskHandles);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void InitView(in float3 cameraPos, in float4x4 matrixProj,in DPlane* planes,in NativeList<JobHandle> taskHandles);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void DispatchSetup(in NativeList<JobHandle> taskHandles,bool isShadow);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void DispatchDraw(CommandBuffer cmdBuffer, in int passIndex,RenderQueue renderQueue,bool UseMotionVectors);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void DispatchDrawShadow(CommandBuffer cmdBuffer, in int passIndex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void Reset();
    }
}