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
        [SerializeField]
        public List<SerializableData> m_gameObjectData;
        public void Init()
        {
            List<DTransform> tempTransform = new List<DTransform>();
            for (int i = 0; i < m_gameObjectData.Count; i++)
            {
                for (int j = 0; j < m_gameObjectData[i].m_LodData.Length; j++)
                {
                    tempTransform.AddRange(m_gameObjectData[i].m_LodData[j].transforms);
                }
            }
            NativeArray<DTransform> transforms = tempTransform.ToNativeArray(Allocator.TempJob);
            var result = new NativeArray<Matrix4x4>(transforms.Length, Allocator.TempJob);
            DInstanceDataJob instanceDataJob = new DInstanceDataJob();
            instanceDataJob.transforms = transforms;
            instanceDataJob.matrix_Worlds = result;
            instanceDataJob.Schedule(transforms.Length, 128).Complete();
            int number = 0;
            for (int i = 0; i < m_gameObjectData.Count; i++)
            {
                for (int j = 0; j < m_gameObjectData[i].m_LodData.Length; j++)
                {
                    List<Matrix4x4> tempMatrix = new List<Matrix4x4>();
                    for (int k = 0; k < m_gameObjectData[i].m_LodData[j].transforms.Length; k++)
                    {
                        tempMatrix.Add(result[number++]);
                    }
                    m_gameObjectData[i].m_LodData[j].originMatrix = tempMatrix.ToArray();
                }
                
            }
            transforms.Dispose();
            result.Dispose();
        }
        public SerializableData GetData(int index)
        {
            return m_gameObjectData[index];
        }
        
    }
}