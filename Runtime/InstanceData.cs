using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.MergeInstancingSystem.CustomData;
using Unity.MergeInstancingSystem.Job;
using Unity.MergeInstancingSystem.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public class InstanceData : ScriptableObject, ISerializationCallbackReceiver
    {

        [SerializeField] public List<Mesh> m_meshs;

        [SerializeField] public List<Material> m_materials;

        [SerializeField] public List<RenderClassState> m_renderClass;
        
        /// <summary>
        /// 矩阵以平移，缩放，旋转的形式序列化，减少内存占用
        /// </summary>
        [SerializeField] public List<DTransform> transforms;

        [SerializeField] public float[] m_LightMapIndexsData;

        [SerializeField] public Vector4[] m_LightMapOffsetsData;

        [NonSerialized] public Matrix4x4[] m_matrix_Worlds;
        public int GetLocalToWorldMatrix4x4Count()
        {
            return m_matrix_Worlds.Length;
        }

        public void Init()
        {
            NativeArray<DTransform> nativeTransforms = transforms.ToNativeArray(Allocator.TempJob);
            NativeArray<Matrix4x4> tempMa = new NativeArray<Matrix4x4>(transforms.Count, Allocator.TempJob);
            //使用多线程将数据还原成矩阵
            var instanceDataJob = new DInstanceDataJob();
            {
                instanceDataJob.transforms = nativeTransforms;
                instanceDataJob.matrix_Worlds = tempMa;

            }
            instanceDataJob.Schedule(transforms.Count,128).Complete();
            m_matrix_Worlds = tempMa.ToArray();
            nativeTransforms.Dispose();
            tempMa.Dispose();
        }
        public void OnBeforeSerialize()
        {

        }

        public void OnAfterDeserialize()
        {

        }

#if UNITY_EDITOR
        [EasyButtons.Button]
        public void CalculateCount()
        {
            int count = 0;
            foreach (var VARIABLE in m_renderClass)
            {
                count += VARIABLE.m_citations;
            }
            EditorUtility.DisplayDialog("结果", $"总矩阵个数为{count}", "确定");
        }
#endif
    }
}