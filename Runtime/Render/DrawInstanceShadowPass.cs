using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
/*
 * 9.22 还差一点搞定
 */
namespace Unity.MergeInstancingSystem.Render
{
    /// <summary>
    /// 阴影渲染需要的所有的值都在前一个pass给提交过了，我们只需要画上去就可以
    /// </summary>
    public class DrawInstanceShadowPass : ScriptableRenderPass
    {
        private List<IRendererInstanceInfo> renderData;
        const string Instance_Shadow = "Render Instance Shadow";
        
        const int k_MaxCascades = 4;
        const int k_ShadowmapBufferBits = 16;
        float m_CascadeBorder;
        float m_MaxShadowDistanceSq;
        int m_ShadowCasterCascadesCount;
        private RenderTexture m_instanceLightShadowmapTexture;
        
        Matrix4x4[] m_MainLightShadowMatrices;
        ShadowSliceData[] m_CascadeSlices;
        Vector4[] m_CascadeSplitDistances;
        
        bool m_CreateEmptyShadowmap;
        
        private static readonly ProfilingSampler InstanceShadow = new ProfilingSampler(Instance_Shadow);
        public DrawInstanceShadowPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            m_MainLightShadowMatrices = new Matrix4x4[k_MaxCascades + 1];
            m_CascadeSlices = new ShadowSliceData[k_MaxCascades];
            m_CascadeSplitDistances = new Vector4[k_MaxCascades];
        }

        public bool Setup(List<IRendererInstanceInfo> data,ref RenderingData renderingData)
        {
            using var profScope = new ProfilingScope(null, InstanceShadow);

            if (!renderingData.shadowData.supportsMainLightShadows)
                return SetupForEmptyRendering(ref renderingData);
            renderData = data;
            Clear();
            // index = -1说明没有光源
            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return SetupForEmptyRendering(ref renderingData);
            
            VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
            Light light = shadowLight.light;
            //主光源如果不开阴影也退出
            if (light.shadows == LightShadows.None)
                return SetupForEmptyRendering(ref renderingData);
            if (shadowLight.lightType != LightType.Directional)
            {
                Debug.LogWarning("Only directional lights are supported as main light.");
            }
            
            //拿到级联阴影的个数
            m_ShadowCasterCascadesCount = renderingData.shadowData.mainLightShadowCascadesCount;
            int shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(renderingData.shadowData.mainLightShadowmapWidth,
                renderingData.shadowData.mainLightShadowmapHeight, m_ShadowCasterCascadesCount);
            renderTargetWidth = renderingData.shadowData.mainLightShadowmapWidth;
            renderTargetHeight = (m_ShadowCasterCascadesCount == 2) ?
                renderingData.shadowData.mainLightShadowmapHeight >> 1 :
                renderingData.shadowData.mainLightShadowmapHeight;
            for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
            {
                //获取级联阴影的矩阵信息
                bool success = ShadowUtils.ExtractDirectionalLightMatrix(ref renderingData.cullResults, ref renderingData.shadowData,
                    shadowLightIndex, cascadeIndex, renderTargetWidth, renderTargetHeight, shadowResolution, light.shadowNearPlane,
                    out m_CascadeSplitDistances[cascadeIndex], out m_CascadeSlices[cascadeIndex]);
                if (!success)
                    return SetupForEmptyRendering(ref renderingData);
            }
            ShadowMapTextureManager.Instance.InitializeTexture(renderTargetWidth, renderTargetHeight);
            m_instanceLightShadowmapTexture = ShadowMapTextureManager.Instance.RenderTexture;
            m_MaxShadowDistanceSq = renderingData.cameraData.maxShadowDistance * renderingData.cameraData.maxShadowDistance;
            m_CascadeBorder = renderingData.shadowData.mainLightShadowCascadeBorder;
            m_CreateEmptyShadowmap = false;
            return true;
        }
        /// <summary>
        /// 不支持阴影的时候,阴影贴图的大小为1，1
        /// </summary>
        /// <param name="renderingData"></param>
        /// <returns></returns>
        bool SetupForEmptyRendering(ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.renderer.stripShadowsOffVariants)
                return false;
            ShadowMapTextureManager.Instance.InitializeTexture(renderTargetWidth, renderTargetHeight);
            m_instanceLightShadowmapTexture =  ShadowMapTextureManager.Instance.RenderTexture;
            m_CreateEmptyShadowmap = true;
            return true;
        }
        /// <summary>
        /// 设置阴影的Rendertarget和CleraFlag，不要清理掉图，因为该Pass用的图是上一个阴影Pass绘制过的
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="cameraTextureDescriptor"></param>
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(new RenderTargetIdentifier(m_instanceLightShadowmapTexture), m_instanceLightShadowmapTexture.depthStencilFormat, renderTargetWidth, renderTargetHeight, 1, true);
            ConfigureClear(ClearFlag.None, Color.black);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
            if (m_CreateEmptyShadowmap)
            {
                SetEmptyMainLightCascadeShadowmap(ref context);
                return;
            }
            var cameraData = renderingData.cameraData;
            var camera = cameraData.camera;
            if (camera != CameraRecognizerManager.ActiveCamera)
                return;
            RenderInstanceShadow(ref context, ref renderingData.cullResults, ref renderingData.lightData, ref renderingData.shadowData);
        }

        /// <summary>
        /// 将渲染用的矩阵都置成单位矩阵
        /// </summary>
        void Clear()
        {
            m_instanceLightShadowmapTexture = null;

            for (int i = 0; i < m_MainLightShadowMatrices.Length; ++i)
                m_MainLightShadowMatrices[i] = Matrix4x4.identity;

            for (int i = 0; i < m_CascadeSplitDistances.Length; ++i)
                m_CascadeSplitDistances[i] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            for (int i = 0; i < m_CascadeSlices.Length; ++i)
                m_CascadeSlices[i].Clear();
        }
        /// <summary>
        /// 设置一些全局使用的参数，前一个Pass设置过了，这里应该不用再设置
        /// </summary>
        /// <param name="context"></param>
        void SetEmptyMainLightCascadeShadowmap(ref ScriptableRenderContext context)
        {
            
        }
       
        void RenderInstanceShadow(ref ScriptableRenderContext context, ref CullingResults cullResults, ref LightData lightData, ref ShadowData shadowData)
        {
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return;
            VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd,InstanceShadow))
            {
                
                for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
                {
                    Vector4 shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowLightIndex, ref shadowData, m_CascadeSlices[cascadeIndex].projectionMatrix, m_CascadeSlices[cascadeIndex].resolution);
                    ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);
                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.CastingPunctualLightShadow, false);
                    cmd.SetGlobalDepthBias(1.0f, 2.5f);
                    RenderShadowSlice(cmd, ref context, ref m_CascadeSlices[cascadeIndex], 
                        m_CascadeSlices[cascadeIndex].projectionMatrix, m_CascadeSlices[cascadeIndex].viewMatrix);
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            
        }

        private bool JudgeDis(float rendererDis)
        {
            if (rendererDis > m_MaxShadowDistanceSq)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public void RenderShadowSlice(CommandBuffer cmd, ref ScriptableRenderContext context,
            ref ShadowSliceData shadowSliceData,
            Matrix4x4 proj, Matrix4x4 view)
        {
            cmd.SetViewport(new Rect(shadowSliceData.offsetX, shadowSliceData.offsetY, shadowSliceData.resolution, shadowSliceData.resolution));
            cmd.SetViewProjectionMatrices(view, proj);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            foreach (var rendererInstanceInfo in renderData)
            {
                if (JudgeDis(rendererInstanceInfo.Dis))
                {
                    continue;
                }
                var pool = rendererInstanceInfo.GetMatrix4x4();
                var poolCountcount = rendererInstanceInfo.GetPoolCount();
                var mesh = rendererInstanceInfo.GetMesh();
                var mat = rendererInstanceInfo.GetMaterial();
                var subMeshIndex = rendererInstanceInfo.GetSubMeshIndex();
                var matBlocks = rendererInstanceInfo.GetMatpropretyBlock();
                for (int i = 0; i < poolCountcount; i++)
                {
                    var count = pool[i].length;
                    var matrixs = pool[i].OnePool;
                    cmd.DrawMeshInstanced(mesh, subMeshIndex, mat, 1, matrixs, count, matBlocks);
                }
            }
            cmd.DisableScissorRect();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            cmd.SetGlobalDepthBias(0.0f, 0.0f);
        }
        /// <summary>
        /// 这里本该释放使用的贴图，但是因为在上一个pass释放过了，这里不做释放
        /// </summary>
        /// <param name="cmd"></param>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (renderData == null)
            {
                return;
            }

            renderData = null;
        }
    }
}