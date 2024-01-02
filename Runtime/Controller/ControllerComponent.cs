using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem
{
    public unsafe abstract class ControllerComponent : MonoBehaviour
    {
        internal static List<ControllerComponent> instanceComponents = new List<ControllerComponent>(8);
        protected static bool SupportInstance;
        protected const int CONSTANTBUFFERSIZE = 16384;
        private static NativeQueue<byte> AviodBugQueue;
        void OnEnable()
        {
            SupportInstance = SystemInfo.maxGraphicsBufferSize > CONSTANTBUFFERSIZE;
            //这里初始化一个NativeQueue,根据之前东哥测试的结果看，闪退的原因是俩个Job在new 一个 queue的时候，QueuePool的初始化出了问题，所以在外部线程里先new一个。
            AviodBugQueue = new NativeQueue<byte>(Allocator.Temp);
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
        public abstract void UpDateTreeWithShadow(in DPlane* planes,in NativeList<JobHandle> taskHandles);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void UpDateTree(in DPlane* planes, in float3 cameraPos, in float4x4 matrixProj, in NativeList<JobHandle> taskHandles);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void DispatchSetup(in NativeList<JobHandle> taskHandles,bool isShadow);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void DispatchDraw(CommandBuffer cmdBuffer, in int passIndex,RenderQueue renderQueue);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void DispatchDrawShadow(CommandBuffer cmdBuffer, in int passIndex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void Reset();
    }
}