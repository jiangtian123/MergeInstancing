using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
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
        [NonSerialized]
        public Texture2D m_matrixs;
        [NonSerialized]
        public Texture2D m_lightMapOffest;
        [SerializeField]
        public TextAsset m_byteMatrixTexture;
        [SerializeField]
        public int matrixTextureW;
        [SerializeField]
        public int matrixTextureH;
        [SerializeField]
        public int OffestTextureW;
        [SerializeField]
        public int OffestTextureH;
        
        [SerializeField]
        public TextAsset m_byteLightOffestTexture;
        [SerializeField]
        public List<int> m_lightMapIndex;
        [NonSerialized]
        public List<Matrix4x4> m_gameObjectMatrix;
        public void Init()
        {
            TextureFormat format = TextureFormat.RGBAFloat;
            m_matrixs= new Texture2D(matrixTextureW, matrixTextureH,format,false);
            byte[] matrixData = m_byteMatrixTexture.bytes;
            m_matrixs.LoadRawTextureData(matrixData);
            m_matrixs.Apply();
            
            byte[] lightData = m_byteLightOffestTexture.bytes;
            m_lightMapOffest= new Texture2D(OffestTextureW, OffestTextureH,format,false);
            m_lightMapOffest.LoadRawTextureData(lightData);
            m_lightMapOffest.Apply();
            
            
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