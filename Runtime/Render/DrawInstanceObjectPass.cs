using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Unity.MergeInstancingSystem.Render
{
    internal unsafe class DrawInstanceObjectPass: ScriptableRenderPass
    {
        RenderTargetIdentifier m_CameraRenderTardent;
        RenderTargetIdentifier[] motionMRT;
        bool isUseMotionVectors;
        const string k_NoMotionVectorInstanceTag = "Render Instance No MotionVectors";
        const string k_HaveMotionVectorInstanceTag = "Render Instance Have MotionVectors";
        private static readonly ProfilingSampler m_ProfilingRenderInstanceNoMotionVectors = new ProfilingSampler(k_NoMotionVectorInstanceTag);
        private static readonly ProfilingSampler m_ProfilingRenderInstanceHaveMotionVectors = new ProfilingSampler(k_HaveMotionVectorInstanceTag);
        float lastTime = 0;
        private readonly int MAX_BUFFCOUNT = 1000;
        public DrawInstanceObjectPass(RenderPassEvent evt)
        {
            isUseMotionVectors = false;
            renderPassEvent = evt;
            motionMRT = new RenderTargetIdentifier[2];
        }
        public void Setup(bool useMotion)
        {
            isUseMotionVectors = useMotion;
        }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            m_CameraRenderTardent = cameraData.renderer.cameraColorTarget;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Profiler.BeginSample("CustomRenderer");
            CommandBuffer cmd = CommandBufferPool.Get();
            var cameraData = renderingData.cameraData;
            var taskHandles = new NativeList<JobHandle>(256, Allocator.Temp);
            var planes = new NativeArray<DPlane>(6, Allocator.TempJob);
            DPlane* planesPtr = (DPlane*)planes.GetUnsafePtr();
            renderingData.cameraData.camera.TryGetCullingParameters(false, out var cullingParams);
            for (var i = 0; i < 6; ++i)
            {
                planes[i] = cullingParams.cameraProperties.GetCameraCullingPlane(i);
            }
            //相机坐标原点
            float3 viewOrigin = renderingData.cameraData.camera.transform.position;
            //投影矩阵
            var matrixProj = cameraData.GetProjectionMatrix();
            //剔除操作
            for (int i = 0; i < ControllerComponent.instanceComponents.Count; i++)
            {
                ControllerComponent.instanceComponents[i].UpDateTree(planesPtr,viewOrigin,matrixProj,taskHandles);
            }
            JobHandle.CompleteAll(taskHandles);
            taskHandles.Clear();
            for (int i = 0; i < ControllerComponent.instanceComponents.Count; i++)
            {
                ControllerComponent.instanceComponents[i].DispatchSetup(taskHandles,false);
            }
            using (new ProfilingScope(cmd, isUseMotionVectors?m_ProfilingRenderInstanceHaveMotionVectors:m_ProfilingRenderInstanceNoMotionVectors))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                for (int i = 0; i < ControllerComponent.instanceComponents.Count; i++)
                {
                    ControllerComponent.instanceComponents[i].DispatchDraw(cmd,0,RenderQueue.Geometry);
                }
                for (int i = 0; i < ControllerComponent.instanceComponents.Count; i++)
                {
                    ControllerComponent.instanceComponents[i].DispatchDraw(cmd,0,RenderQueue.AlphaTest);
                }
                for (int i = 0; i < ControllerComponent.instanceComponents.Count; i++)
                {
                    ControllerComponent.instanceComponents[i].DispatchDraw(cmd,0,RenderQueue.Transparent);
                }
            }
            //放下面
            Profiler.EndSample();
            taskHandles.Dispose();
            planes.Dispose();
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            for (int i = 0; i < ControllerComponent.instanceComponents.Count; i++)
            {
                ControllerComponent.instanceComponents[i].Reset();
            }
        }
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            
        }
    }
}