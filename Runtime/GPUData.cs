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
    public struct GPUIndex
    {
        public int ObjMatrixIndex;
        public int MeshMatrixIndex;
        public int LightDataIndex;

        public GPUIndex(int oIndex,int mIndex,int lIndex = -1)
        {
            ObjMatrixIndex = oIndex;
            MeshMatrixIndex = mIndex;
            LightDataIndex = lIndex;
        }
    }

    public struct MatrixWithInvMatrix
    {
        public Matrix4x4 matrix;
        public Matrix4x4 invmatrix;
    }
}