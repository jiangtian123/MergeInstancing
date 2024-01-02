using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem
{
    public unsafe class GrassController : ControllerComponent
    {
        
        protected override void OnRegiste()
        {
        }

        protected override void UnRegiste()
        {
            
        }

        #region Rendering
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void UpDateTreeWithShadow(in DPlane* planes, in NativeList<JobHandle> taskHandles)
        {
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void UpDateTree(in DPlane* planes, in float3 cameraPos, in float4x4 matrixProj,
            in NativeList<JobHandle> taskHandles)
        {
            
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void DispatchSetup(in NativeList<JobHandle> taskHandles, bool isShadow)
        {
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void DispatchDraw(CommandBuffer cmdBuffer, in int passIndex, RenderQueue renderQueue)
        {
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void DispatchDrawShadow(CommandBuffer cmdBuffer, in int passIndex)
        {
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Reset()
        {
            
        }

        #endregion
      
    }
}