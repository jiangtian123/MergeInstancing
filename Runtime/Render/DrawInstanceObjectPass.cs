using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.CustomData;
using Unity.MergeInstancingSystem.New;
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
        private LayerMask layer;
        const string k_NoMotionVectorInstanceTag = "Render Instance No MotionVectors";
        const string k_HaveMotionVectorInstanceTag = "Render Instance Have MotionVectors";
        private static readonly ProfilingSampler m_ProfilingRenderInstanceNoMotionVectors = new ProfilingSampler(k_NoMotionVectorInstanceTag);
        private static readonly ProfilingSampler m_ProfilingRenderInstanceHaveMotionVectors = new ProfilingSampler(k_HaveMotionVectorInstanceTag);
        float lastTime = 0;
        private readonly int MAX_BUFFCOUNT = 1000;
        public RenderQueue m_renderqueue;
        public DrawInstanceObjectPass(RenderPassEvent evt)
        {
            isUseMotionVectors = false;
            renderPassEvent = evt;
            motionMRT = new RenderTargetIdentifier[2];
        }
        public void Setup(bool useMotion, LayerMask layer,RenderQueue renderQueue)
        {
            m_renderqueue = renderQueue;
            isUseMotionVectors = useMotion;
            this.layer = layer;
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
            var camera = cameraData.camera;
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
            float preRelative = 0;
            if (camera.orthographic)
            {
                preRelative = 0.5f / camera.orthographicSize;
            }
            else
            {
                float halfAngle = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5F);
                preRelative = 0.5f / halfAngle;
            }
            preRelative = preRelative * QualitySettings.lodBias;
            //剔除操作
            for (int i = 0; i < ControllerComponent.instanceComponents.Count; i++)
            {
                ControllerComponent.instanceComponents[i].UpDateTree(planesPtr,viewOrigin,matrixProj,preRelative,taskHandles);
            }
            JobHandle.CompleteAll(taskHandles);
            taskHandles.Clear();
            for (int i = 0; i < ControllerComponent.instanceComponents.Count; i++)
            {
                ControllerComponent.instanceComponents[i].InitView(viewOrigin,matrixProj,planesPtr,taskHandles);
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
                    ControllerComponent.instanceComponents[i].DispatchDraw(cmd,0,m_renderqueue,isUseMotionVectors);
                }
            }
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
        void RenderWithMotionVectors(CommandBuffer cmd, RenderingData data, Camera camera, CameraData cameraData)
        {
            // motionMRT[0] = m_CameraRenderTardent;
            // motionMRT[1] =  MotionVectorTexManager.instance.GetRenderTex().Identifier();
            // cmd.SetRenderTarget(motionMRT, motionMRT[1], 0, CubemapFace.Unknown, -1);
            // var time = lastTime;
            // Vector4 lastTimeVector = time * new Vector4(1f / 20f, 1f, 2f, 3f);
            // foreach (var rendererInstanceInfo in renderData)
            // {
            //     var matrix4x4pool = rendererInstanceInfo.GetMatrix4x4();
            //     var poolCountcount = rendererInstanceInfo.GetPoolCount();
            //     var mesh = rendererInstanceInfo.GetMesh();
            //     var mat = rendererInstanceInfo.GetMaterial();
            //     var subMeshIndex = rendererInstanceInfo.GetSubMeshIndex();
            //     var matBlocks = rendererInstanceInfo.GetMatpropretyBlock();
            //     var useLightMap = rendererInstanceInfo.UseLightMapOrProbe;
            //     mat.EnableKeyword("CUSTOM_INSTANCING_ON");
            //     mat.EnableKeyword("_USEMOTIONVECTOR");
            //     mat.SetVector("_LastFrameTime", lastTimeVector);
            //     for (int i = 0; i < poolCountcount; i++)
            //     {
            //         matBlocks.Clear();
            //         if (useLightMap)
            //         {
            //             var lightMapIndexpool = rendererInstanceInfo.GetlightMapIndex();
            //             var lightScalOffsetpool = rendererInstanceInfo.GetlightMapScaleOffset();
            //             mat.EnableKeyword("CUSTOM_LIGHTMAP_ON");
            //             matBlocks.SetFloatArray("_lightMapIndex", lightMapIndexpool[i].OnePool);
            //             matBlocks.SetVectorArray("_LightScaleOffset", lightScalOffsetpool[i].OnePool);
            //         }
            //         var count = matrix4x4pool[i].length;
            //         var matrixs = matrix4x4pool[i].OnePool;
            //         cmd.DrawMeshInstanced(mesh, subMeshIndex, mat, 0, matrixs, count, matBlocks);
            //     }
            // }
            // lastTime = Time.time;
        }
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            
        }
    }
}