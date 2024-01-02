using System;
using Unity.MergeInstancingSystem;
using UnityEngine;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public class LodSerializableData
    {
        [SerializeField]
        public DTransform[] transforms;
        [NonSerialized]
        public Matrix4x4[] originMatrix;
        [SerializeField]
        public float[] lightmapIndex;
        [SerializeField]
        public Vector4[] lightmapOffest;
    }
}