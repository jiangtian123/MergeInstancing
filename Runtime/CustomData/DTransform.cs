using System;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.MergeInstancingSystem
{
    
    [Serializable]
    public struct DTransform
    {
        public float3 position;
        public Quaternion rotation;
        public float3 scale;

        public DTransform(float3 position, Quaternion rotation, float3 scale)
        {
            this.scale = scale;
            this.rotation = rotation;
            this.position = position;
        }
        public DTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.scale = scale;
            this.rotation = rotation;
            this.position = position;
        }
        public static implicit operator Matrix4x4(DTransform matrix4)
        {
           return Matrix4x4.TRS(matrix4.position, matrix4.rotation, matrix4.scale);
        }
    }
}