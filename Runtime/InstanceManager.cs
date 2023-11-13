using System.Collections.Generic;
using Unity.MergeInstancingSystem.Controller;
using Unity.MergeInstancingSystem.Render;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem
{
    /// <summary>
    /// 中转站，沟通Mono和Feature
    /// </summary>
    public class InstanceManager
    {
        #region Singleton

        private static InstanceManager m_instance = null;
        private bool IsSRP => GraphicsSettings.renderPipelineAsset != null || QualitySettings.renderPipeline != null;
        
        /// <summary>
        /// 控制相机两帧朝向偏移值
        /// </summary>
        public float lookThreshold = 0.00004f;
        /// <summary>
        /// 控制相机两帧位置偏移值
        /// </summary>
        public float posThreshold = 16.0f;

        private CameraConfig activeCameraData;

        private bool CurrentIsNeedUpData;
        public static InstanceManager Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new InstanceManager();
                }

                return m_instance;
            }
        }
        #endregion

        InstanceManager()
        {
            activeCameraData = new CameraConfig();
            CurrentIsNeedUpData = true;
        }

        #region Method

        public List<InstanceControllerBase> ActiveControllers
        {
            get
            {
                if (m_activeControllers == null)
                {
                    m_activeControllers = new List<InstanceControllerBase>();
                    if (IsSRP)
                    {
                        RenderPipelineManager.beginCameraRendering += OnPreCull;
                    }

                    else
                    {
                        Camera.onPreCull += OnPreCull;
                    }
                        
                }
                return m_activeControllers;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="controller"></param>
        public void Register(InstanceControllerBase controller)
        {
            activeCameraData.pos = Vector3.zero;
            activeCameraData.lookat = Vector3.zero;
            useMotionvector = controller.useMotionVector;
            ActiveControllers.Add(controller);
        }
        public void Unregister(InstanceControllerBase controller)
        {
            activeCameraData.pos = Vector3.zero;
            activeCameraData.lookat = Vector3.zero;
            ActiveControllers.Remove(controller);
        }
        public void RegisterOpaqueRenderlist(IRendererInstanceInfo instanceInfo)
        {
            m_OpaqueRenderlist.Add(instanceInfo);
        }
        public void RegisterShadowRenderlist(IRendererInstanceInfo instanceInfo)
        {
            m_ShadowRenderlist.Add(instanceInfo);
        }
        public void RegisterTransparentRenderlist(IRendererInstanceInfo instanceInfo)
        {
            m_TransparentRenderlist.Add(instanceInfo);
        }

        public void RegisterLightMap(Texture2DArray lightMap)
        {
            m_lightMapArray = lightMap;
        }
        public void RegisterShadowMask(Texture2DArray lightMap)
        {
            m_ShadowMaskArray = lightMap;
        }
        #endregion
        

        #region variables

        public bool useMotionvector = false;

        public bool useInstance
        {
            get
            {
                if (m_activeControllers == null || m_activeControllers.Count == 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
        
        private List<InstanceControllerBase> m_activeControllers = null;
        // ------------------ 每帧都需要清空 --------------------------------------------------------------------
        private List<IRendererInstanceInfo> m_OpaqueRenderlist = new List<IRendererInstanceInfo>();
        private List<IRendererInstanceInfo> m_ShadowRenderlist = new List<IRendererInstanceInfo>();
        private List<IRendererInstanceInfo> m_TransparentRenderlist = new List<IRendererInstanceInfo>();
        
        
        // ------------------- 整个场景只有两个光照贴图 -----------------------------------------------------------
        private Texture2DArray m_lightMapArray = null;
        private Texture2DArray m_ShadowMaskArray = null;
        
        
        
        public List<IRendererInstanceInfo> OpaqueRenderlist
        {
            get
            {
                return m_OpaqueRenderlist;
            }
        }
        public List<IRendererInstanceInfo> ShadowRenderlist
        {
            get
            {
                return m_ShadowRenderlist;
            }
        }
        public List<IRendererInstanceInfo> TransparentRenderlist
        {
            get
            {
                return m_TransparentRenderlist;
            }
        }
        
        public Texture2DArray LightMapArray
        {
            get
            {
                return m_lightMapArray;
            }
        }
        public Texture2DArray ShadowMaskArray
        {
            get
            {
                return m_ShadowMaskArray;
            }
        }

        #endregion
        
        internal class CameraConfig
        {
            public Vector3 pos;
            public Vector3 lookat;
        }

        private  void CheckOutNeedUpdata(Camera camera)
        {
            var currentPos = camera.transform.position;
            var currentLookat = Vector3.Normalize(camera.transform.forward);
            var lastPos = activeCameraData.pos;
            var lastLookat = activeCameraData.lookat;
            var deltaPos = Vector3.SqrMagnitude(currentPos - lastPos);
            var deltaLook = Vector3.SqrMagnitude(currentLookat - lastLookat);
            if (deltaPos > posThreshold || deltaLook > lookThreshold)
            {
                activeCameraData.pos = currentPos;
                activeCameraData.lookat = currentLookat;
                CurrentIsNeedUpData =  true;
            }
            else
            {
                CurrentIsNeedUpData =  false;
            }
        }

        private void OnPreCull(ScriptableRenderContext context, Camera cam)
        {
            OnPreCull(cam);
        }

        public void OnPreCull(Camera cam)
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying == false)
            {
                if (SceneView.currentDrawingSceneView == null)
                    return;
                if (cam != SceneView.currentDrawingSceneView.camera)
                    return;
            }
            else
            {
                if (cam != CameraRecognizerManager.ActiveCamera)
                    return;
            }
#else
            if (cam != CameraRecognizerManager.ActiveCamera)
                return;
#endif
            // CheckOutNeedUpdata(cam);
            // if (m_activeControllers == null || !CurrentIsNeedUpData)
            //     return;
            OnReset(cam);
            for (int i = 0; i < m_activeControllers.Count; ++i)
            {
                m_activeControllers[i].UpdateCull(cam);
            }
            
        }
        
        //-------------------------- 渲染结束后，清理渲染数据 ---------------------------------------------
        public void OnReset(Camera cam)
        {
            for (int i = 0; i < m_activeControllers.Count; ++i)
            {
                m_activeControllers[i].ResetRenderInfo();
            }
            m_OpaqueRenderlist.Clear();
            m_ShadowRenderlist.Clear();
            m_TransparentRenderlist.Clear();
        }
    }
}