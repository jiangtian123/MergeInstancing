using System;
using Unity.MergeInstancingSystem.CustomData;
using UnityEngine;

namespace Unity.MergeInstancingSystem.New
{
    [Serializable]
    public class SerializableData
    {
        [SerializeField]
        public LodSerializableData[] m_LodData; 
    }
}