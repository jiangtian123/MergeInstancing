using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.MergeInstancingSystem.Render
{
    /*
     * 
     */
    public class InstanceRenderFeature: ScriptableRendererFeature
    {
        private DrawInstanceShadowPass m_shadowPass;
        private DrawInstanceObjectPass m_OpaquePass;

        public LayerMask layer;
        public override void Create()
        {
            m_OpaquePass = new DrawInstanceObjectPass(RenderPassEvent.AfterRenderingOpaques);
            m_shadowPass = new DrawInstanceShadowPass(RenderPassEvent.BeforeRenderingShadows+1);
        }
        private bool CheckCullingMask(int mask, int layer)
        {
            return (mask & (1 << layer)) != 0;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Application.isPlaying == false) { return; }
            if (!CheckCullingMask(renderingData.cameraData.camera.cullingMask, layer))
            {
                return;
            }
            var stack = VolumeManager.instance.stack;
            var Taa = stack.GetComponent<TAAVolume>();
            bool applyPostProcessing = renderingData.cameraData.postProcessEnabled;
            bool isMotionVector = false;
            if (Taa.IsActive() && applyPostProcessing)
            {
                if ((TAAQuality)Taa.quality == TAAQuality.HIGH)
                {
                    isMotionVector = true;
                }
            }
            InitRenderData();
            if (m_shadowPass.Setup(ref renderingData))
            {
                renderer.EnqueuePass(m_shadowPass);
            }
            m_OpaquePass.Setup(isMotionVector);
            renderer.EnqueuePass(m_OpaquePass);
        }

        void InitRenderData()
        {
            Shader.SetGlobalTexture("_InstanceLightMapArray",ConvertToTexture2Darray.Instance.GetLightMapArray());
            Shader.SetGlobalTexture("_InstanceShadowMaskArray",ConvertToTexture2Darray.Instance.GetShadowMaskArray());
            Shader.SetGlobalVector("instance_SHAr", CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientSkyColor));
            Shader.SetGlobalVector("instance_SHAg", CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientEquatorColor));
            Shader.SetGlobalVector("instance_SHAb", CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientGroundColor));
        }
    }
}