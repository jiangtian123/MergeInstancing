using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Unity.MergeInstancingSystem.Render
{
    public class DrawInstanceObjectPass: ScriptableRenderPass
    {
        RenderTargetIdentifier m_CameraRenderTardent;
        RenderTargetIdentifier[] motionMRT;
        bool isUseMotionVectors;
        private LayerMask layer;
        const string k_NoMotionVectorInstanceTag = "Render Instance No MotionVectors";
        const string k_HaveMotionVectorInstanceTag = "Render Instance Have MotionVectors";
        private static readonly ProfilingSampler m_ProfilingRenderInstanceNoMotionVectors = new ProfilingSampler(k_NoMotionVectorInstanceTag);
        private static readonly ProfilingSampler m_ProfilingRenderInstanceHaveMotionVectors = new ProfilingSampler(k_HaveMotionVectorInstanceTag);
        private List<IRendererInstanceInfo> renderData;
        float lastTime = 0;
        public DrawInstanceObjectPass(RenderPassEvent evt)
        {
            isUseMotionVectors = false;
            renderPassEvent = evt;
            motionMRT = new RenderTargetIdentifier[2];
        }

        public void Setup(bool useMotion, List<IRendererInstanceInfo> data, LayerMask layer)
        {
            isUseMotionVectors = useMotion;
            renderData = data;
            this.layer = layer;
        }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            m_CameraRenderTardent = cameraData.renderer.cameraColorTarget;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            var cameraData = renderingData.cameraData;
            var camera = cameraData.camera;
            if (camera != CameraRecognizerManager.ActiveCamera)
                return;
            using (new ProfilingScope(cmd, isUseMotionVectors?m_ProfilingRenderInstanceHaveMotionVectors:m_ProfilingRenderInstanceNoMotionVectors))
            {
                var lightMapArray = InstanceManager.Instance.LightMapArray;
                var shadowMaskArray = InstanceManager.Instance.ShadowMaskArray;
                if (lightMapArray!=null)
                {
                    cmd.SetGlobalTexture("_InstanceLightMapArray",lightMapArray);
                }
                if (shadowMaskArray!=null)
                {
                    cmd.SetGlobalTexture("_InstanceShadowMaskArray",shadowMaskArray);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                Profiler.BeginSample("Begin Rendering");
                if (isUseMotionVectors)
                {
                    RenderWithMotionVectors(cmd, renderingData, camera, cameraData);
                }
                else
                {
                    RenderNoMotionVectors(cmd, renderingData);
                }
                Profiler.EndSample();
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        /*
         * 2023.9.27树渲染少的原因为提交的少
         * 加一个优化，不适用LightMap时，就只提交环境光照,不提交LightProbe
         */
        void RenderNoMotionVectors(CommandBuffer cmd, RenderingData data)
        {
            //一个rendererInstanceInfo 是一种mesh加材质类型的DC
            foreach (var rendererInstanceInfo in renderData)
            {
                var matrix4x4pool = rendererInstanceInfo.GetMatrix4x4();
                var poolCountcount = rendererInstanceInfo.GetPoolCount();
                var mesh = rendererInstanceInfo.GetMesh();
                var mat = rendererInstanceInfo.GetMaterial();
                var subMeshIndex = rendererInstanceInfo.GetSubMeshIndex();
                var matBlocks = rendererInstanceInfo.GetMatpropretyBlock();
                var useLightMap = rendererInstanceInfo.UseLightMapOrProbe;
                mat.EnableKeyword("CUSTOM_INSTANCING_ON");
                //poolCountcount 是因为一种类型的物体如果超过了1000，就要分批次提交
                for (int i = 0; i < poolCountcount; i++)
                {
                    matBlocks.Clear();
                    if (useLightMap)
                    {
                        var lightMapIndexpool = rendererInstanceInfo.GetlightMapIndex();
                        var lightScalOffsetpool = rendererInstanceInfo.GetlightMapScaleOffset();
                        mat.EnableKeyword("CUSTOM_LIGHTMAP_ON");
                        matBlocks.SetFloatArray("_lightMapIndex",lightMapIndexpool[i].OnePool);
                        matBlocks.SetVectorArray("_LightScaleOffset",lightScalOffsetpool[i].OnePool);
                    }
                    var count = matrix4x4pool[i].length;
                    var matrixs = matrix4x4pool[i].OnePool;
                    cmd.DrawMeshInstanced(mesh, subMeshIndex, mat, 0, matrixs, count, matBlocks);
                }
            }
        }
    
        void RenderWithMotionVectors(CommandBuffer cmd, RenderingData data, Camera camera, CameraData cameraData)
        {
            motionMRT[0] = m_CameraRenderTardent;
            motionMRT[1] =  MotionVectorTexManager.instance.GetRenderTex().Identifier();
            cmd.SetRenderTarget(motionMRT, motionMRT[1], 0, CubemapFace.Unknown, -1);
            var time = lastTime;
            Vector4 lastTimeVector = time * new Vector4(1f / 20f, 1f, 2f, 3f);
            foreach (var rendererInstanceInfo in renderData)
            {
                var matrix4x4pool = rendererInstanceInfo.GetMatrix4x4();
                var poolCountcount = rendererInstanceInfo.GetPoolCount();
                var mesh = rendererInstanceInfo.GetMesh();
                var mat = rendererInstanceInfo.GetMaterial();
                var subMeshIndex = rendererInstanceInfo.GetSubMeshIndex();
                var matBlocks = rendererInstanceInfo.GetMatpropretyBlock();
                var useLightMap = rendererInstanceInfo.UseLightMapOrProbe;
                mat.EnableKeyword("CUSTOM_INSTANCING_ON");
                mat.EnableKeyword("_USEMOTIONVECTOR");
                mat.SetVector("_LastFrameTime", lastTimeVector);
                for (int i = 0; i < poolCountcount; i++)
                {
                    matBlocks.Clear();
                    if (useLightMap)
                    {
                        var lightMapIndexpool = rendererInstanceInfo.GetlightMapIndex();
                        var lightScalOffsetpool = rendererInstanceInfo.GetlightMapScaleOffset();
                        mat.EnableKeyword("CUSTOM_LIGHTMAP_ON");
                        matBlocks.SetFloatArray("_lightMapIndex", lightMapIndexpool[i].OnePool);
                        matBlocks.SetVectorArray("_LightScaleOffset", lightScalOffsetpool[i].OnePool);
                    }
                    var count = matrix4x4pool[i].length;
                    var matrixs = matrix4x4pool[i].OnePool;
                    cmd.DrawMeshInstanced(mesh, subMeshIndex, mat, 0, matrixs, count, matBlocks);
                }
            }
            lastTime = Time.time;
        }
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            renderData = null;
        }
    }
}