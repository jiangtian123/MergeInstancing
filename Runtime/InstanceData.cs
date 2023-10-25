using System;
using System.Collections.Generic;
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

        [SerializeField] public UnityEngine.Matrix4x4[] m_Matrix4X4sData;

        [SerializeField] public float[] m_LightMapIndexsData;

        [SerializeField] public Vector4[] m_LightMapOffsetsData;

        public int GetLocalToWorldMatrix4x4Count()
        {
            return m_Matrix4X4sData.Length;
        }

        public void BatchMaterial()
        {

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