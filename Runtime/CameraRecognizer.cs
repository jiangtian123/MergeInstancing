using System;
using UnityEngine;
namespace Unity.MergeInstancingSystem
{
    public class CameraRecognizer : MonoBehaviour
    {
        private Camera m_recognizedCamera;

        public Plane[] planes;
        public Camera RecognizedCamera => m_recognizedCamera;

        [SerializeField]
        private int m_id;
        [SerializeField]
        private int m_priority;


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
            
            m_recognizedCamera = GetComponent<Camera>();
            planes = GeometryUtility.CalculateFrustumPlanes(m_recognizedCamera);
        }
        private void OnEnable()
        {
            CameraRecognizerManager.Instance.RegisterRecognizer(this);
        }

        private void Update()
        {
            planes = GeometryUtility.CalculateFrustumPlanes(m_recognizedCamera);
        }

        private void OnDisable()
        {
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