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
            CheckOutNeedUpdata(cam);
            if (m_activeControllers == null || !CurrentIsNeedUpData)
                return;
            OnDispose(cam);
            for (int i = 0; i < m_activeControllers.Count; ++i)
            {
                m_activeControllers[i].UpdateCull(cam);
            }
            //m_OpaqueRender.DrawInstance(m_OpaqueRenderlist,cam);
        }
        
        //-------------------------- 渲染结束后，清理渲染数据 ---------------------------------------------
        public void OnDispose(Camera cam)
        {
            for (int i = 0; i < m_activeControllers.Count; ++i)
            {
                m_activeControllers[i].Dispose();
            }
            m_OpaqueRenderlist.Clear();
            m_ShadowRenderlist.Clear();
            m_TransparentRenderlist.Clear();
        }
#if  UNITY_EDITOR
        public void Render(Bounds bounds, Color color, float width)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] m_allocatedVertices = new Vector3[8];
            
            m_allocatedVertices[0] = new Vector3(min.x, min.y, min.z);
            m_allocatedVertices[1] = new Vector3(min.x, min.y, max.z);
            m_allocatedVertices[2] = new Vector3(max.x, min.y, max.z);
            m_allocatedVertices[3] = new Vector3(max.x, min.y, min.z);

            m_allocatedVertices[4] = new Vector3(min.x, max.y, min.z);
            m_allocatedVertices[5] = new Vector3(min.x, max.y, max.z);
            m_allocatedVertices[6] = new Vector3(max.x, max.y, max.z);
            m_allocatedVertices[7] = new Vector3(max.x, max.y, min.z);
            
            Handles.color = color;

            Handles.DrawLine(m_allocatedVertices[0], m_allocatedVertices[1], width);
            Handles.DrawLine(m_allocatedVertices[1], m_allocatedVertices[2], width);
            Handles.DrawLine(m_allocatedVertices[2], m_allocatedVertices[3], width);
            Handles.DrawLine(m_allocatedVertices[3], m_allocatedVertices[0], width);

            Handles.DrawLine(m_allocatedVertices[0], m_allocatedVertices[4], width);
            Handles.DrawLine(m_allocatedVertices[1], m_allocatedVertices[5], width);
            Handles.DrawLine(m_allocatedVertices[2], m_allocatedVertices[6], width);
            Handles.DrawLine(m_allocatedVertices[3], m_allocatedVertices[7], width);

            Handles.DrawLine(m_allocatedVertices[4], m_allocatedVertices[5], width);
            Handles.DrawLine(m_allocatedVertices[5], m_allocatedVertices[6], width);
            Handles.DrawLine(m_allocatedVertices[6], m_allocatedVertices[7], width);
            Handles.DrawLine(m_allocatedVertices[7], m_allocatedVertices[4], width);
        }
        void DnBugCull(Camera camera)
        {
            RendererBounds(m_OpaqueRenderlist,camera);
            RendererBounds(m_TransparentRenderlist,camera);
        }

        void RendererBounds(List<IRendererInstanceInfo> rendererInstanceInfo ,Camera camera )
        {
            foreach (var render in rendererInstanceInfo)
            {
                var matrix4x4pool = render.GetMatrix4x4();
                var poolCountcount = render.GetPoolCount();
                var mesh = render.GetMesh();
                for (int i = 0; i < poolCountcount; i++)
                {
                    var count = matrix4x4pool[i].length;
                    var matrixs = matrix4x4pool[i].OnePool;
                    for (int j = 0; j < count; j++)
                    {
                        var matrix4X4 = matrixs[i];
                        Bounds localBounds = new Bounds();
                        if (mesh.bounds != null)
                        {
                            localBounds = mesh.bounds;
                        }
                        else
                        {
                            Debug.Log(mesh.name);
                        }

                        Bounds worldBounds = TransformBoundsToWorldBounds(matrix4X4, localBounds);
                        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
                        Color _color = Color.gray;
                        if (GeometryUtility.TestPlanesAABB(planes, worldBounds))
                        {
                            _color = Color.yellow;
                        }
                        else
                        {
                            _color = Color.red;
                        }

                        Render(worldBounds, _color, 2.0f);
                    }
                }
            }
        }
        Bounds TransformBoundsToWorldBounds(UnityEngine.Matrix4x4 matrix, Bounds localBounds)
        {
            Bounds bounds = localBounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] points = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z),
            };

            for (int i = 0; i < points.Length; ++i)
            {
                points[i] = matrix.MultiplyPoint(points[i]);
            }

            Vector3 newMin = points[0];
            Vector3 newMax = points[0];

            for (int i = 1; i < points.Length; ++i)
            {
                if (newMin.x > points[i].x) newMin.x = points[i].x;
                if (newMax.x < points[i].x) newMax.x = points[i].x;
                
                if (newMin.y > points[i].y) newMin.y = points[i].y;
                if (newMax.y < points[i].y) newMax.y = points[i].y;
                
                if (newMin.z > points[i].z) newMin.z = points[i].z;
                if (newMax.z < points[i].z) newMax.z = points[i].z;
            }


            Bounds newBounds = new Bounds();
            newBounds.SetMinMax(newMin, newMax);
            return newBounds;
        }
#endif
    }
}