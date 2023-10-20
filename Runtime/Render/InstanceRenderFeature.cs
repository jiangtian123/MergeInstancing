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
        private DrawInstanceObjectPass m_TransparentPass;

        public LayerMask layer;
        public override void Create()
        {
            m_OpaquePass = new DrawInstanceObjectPass(RenderPassEvent.AfterRenderingOpaques);
            m_shadowPass = new DrawInstanceShadowPass(RenderPassEvent.BeforeRenderingShadows+1);
            m_TransparentPass = new DrawInstanceObjectPass(RenderPassEvent.BeforeRenderingTransparents);
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!InstanceManager.Instance.useInstance)
            {
                return;
            }
            var stack = VolumeManager.instance.stack;
            var Taa = stack.GetComponent<TAAVolume>();
            bool applyPostProcessing = renderingData.cameraData.postProcessEnabled;
            bool isMotionVector = false;
            if (Taa.IsActive() && applyPostProcessing)
            {
                if ((TAAQuality)Taa.quality == TAAQuality.HIGH && InstanceManager.Instance.useMotionvector)
                {
                    isMotionVector = true;
                }
            }

            InitRenderData();
            if (m_shadowPass.Setup(InstanceManager.Instance.ShadowRenderlist, ref renderingData))
            {
                renderer.EnqueuePass(m_shadowPass);
            }
            m_OpaquePass.Setup(isMotionVector, InstanceManager.Instance.OpaqueRenderlist, layer);
            m_TransparentPass.Setup(isMotionVector, InstanceManager.Instance.TransparentRenderlist, layer);
            renderer.EnqueuePass(m_OpaquePass);
            renderer.EnqueuePass(m_TransparentPass);
        }

        void InitRenderData()
        {
            Shader.SetGlobalVector("instance_SHAr", CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientSkyColor));
            Shader.SetGlobalVector("instance_SHAg", CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientEquatorColor));
            Shader.SetGlobalVector("instance_SHAb", CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientGroundColor));
        }
    }
}