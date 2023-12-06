using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.CustomData;
using Unity.MergeInstancingSystem.New;
using Unity.MergeInstancingSystem.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.MergeInstancingSystem
{
  

    [Serializable]
    public class InstanceData : ScriptableObject
    {
        /// <summary>
        /// 序列化存的每个Gameobject对应的简化矩阵
        /// </summary>
        [SerializeField]
        public List<DTransform> m_gameobjTransform;
        /// <summary>
        /// 序列化存的每个Prefab对应的简化矩阵
        /// </summary>
        [SerializeField]
        public List<Matrix4x4> m_prefabMatrixs;
        [SerializeField]
        public List<InstanceLightData> m_lightData;
        
        [NonSerialized]
        public List<Matrix4x4> m_gameObjectMatrix;
        public void Init()
        {
            m_gameObjectMatrix = new List<Matrix4x4>();
            NativeArray<DTransform> objtransforms = m_gameobjTransform.ToNativeArray(Allocator.TempJob);
            var pbjresult = new NativeArray<Matrix4x4>(objtransforms.Length, Allocator.TempJob);
            
            DInstanceDataJob instanceDataJob = new DInstanceDataJob();
            instanceDataJob.transforms = objtransforms;
            instanceDataJob.matrix_Worlds = pbjresult; 
            instanceDataJob.Schedule(objtransforms.Length, 128).Complete();
            m_gameObjectMatrix.AddRange(pbjresult.ToArray());
            objtransforms.Dispose();
            pbjresult.Dispose();
        }
    }
}