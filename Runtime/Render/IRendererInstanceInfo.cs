using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.MergeInstancingSystem.Pool;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.Render
{
    public interface IRendererInstanceInfo
    {
        Mesh GetMesh();
        Material GetMaterial();
        List<Pool<Matrix4x4>> GetMatrix4x4();
        List<Pool<float>> GetlightMapIndex();
        List<Pool<Vector4>> GetlightMapScaleOffset();
        MaterialPropertyBlock GetMatpropretyBlock();
        /// <summary>
        /// 当前几个Pool在使用
        /// </summary>
        /// <returns></returns>
        int GetPoolCount();
        int GetSubMeshIndex();

        bool UseLightMapOrProbe
        {
            get;
        }

        float Dis
        {
            get;
        }
        void Dispose();
    }
}