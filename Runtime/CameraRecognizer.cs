using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.CustomData;
using UnityEngine;
namespace Unity.MergeInstancingSystem
{
    public class CameraRecognizer : MonoBehaviour
    {
        private Camera m_recognizedCamera;

        public NativeArray<DPlane> planes;
        public Camera RecognizedCamera => m_recognizedCamera;

        [SerializeField]
        private int m_id;
        [SerializeField]
        private int m_priority;

        public float preRelative;

        public Vector3 cameraPos;
        public int ID
        {
            get
            {
                return m_id;
            }
        }

        public int Priority
        {
            get
            {
                return m_priority;
            }
        }
        
        

        private void Awake()
        {
            planes = new NativeArray<DPlane>(6, Allocator.Persistent);
            m_recognizedCamera = GetComponent<Camera>();
            var cameraPlanes = GeometryUtility.CalculateFrustumPlanes(m_recognizedCamera);
            for (int i = 0; i < cameraPlanes.Length; i++)
            {
                planes[i] = cameraPlanes[i];
            }
        }
        private void OnEnable()
        {
            CameraRecognizerManager.Instance.RegisterRecognizer(this);
        }

        private void Update()
        {
            var cameraPlanes = GeometryUtility.CalculateFrustumPlanes(m_recognizedCamera);
            for (int i = 0; i < cameraPlanes.Length; i++)
            {
                planes[i] = cameraPlanes[i];
            }
            UpdateCamera();
        }
        public void UpdateCamera()
        {
            if (m_recognizedCamera.orthographic)
            {
                preRelative = 0.5f / m_recognizedCamera.orthographicSize;
            }
            else
            {
                float halfAngle = Mathf.Tan(Mathf.Deg2Rad * m_recognizedCamera.fieldOfView * 0.5F);
                preRelative = 0.5f / halfAngle;
            }
            preRelative = preRelative * QualitySettings.lodBias;
            cameraPos = m_recognizedCamera.transform.position;
        }
        private void OnDisable()
        {
            planes.Dispose();
            CameraRecognizerManager.Instance.UnregisterRecognizer(this);            
        }
        public void Active()
        {
            if (enabled == false)
            {
                Debug.LogError("Failed to active HLODCameraRecognizer. It is not Enabled.");
                return;
            }

            CameraRecognizerManager.Instance.Active(this);
        }
    }
}