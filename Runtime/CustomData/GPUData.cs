using System;
using UnityEngine;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public struct InstanceLightData
    {
        [SerializeField]
        public Vector4 m_lightOffest;
        [SerializeField]
        public int m_lightIndex;
    }
}