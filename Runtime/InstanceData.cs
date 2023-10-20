using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public class InstanceData :ScriptableObject, ISerializationCallbackReceiver
    {
        [Serializable]
        public struct MaterialPropertyData
        {
            public int material_ID;
            public string name;
            public Byte[] m_vaule;
        }
        
        [SerializeField]
        public List<Mesh> m_meshs;
        /// <summary>
        /// 材质的引用次数
        /// </summary>
        [SerializeField]
        public int[] m_matCitationNumber;
        
        [SerializeField]
        public List<Material> m_materials;
        
        [SerializeField]
        public Matrix4x4[] m_localToWorlds;

        [SerializeField] 
        public List<int> m_matAndMeshIdentifiers;
    
        [SerializeField]
        public List<int> m_identifierCounts;
        
        /// <summary>
        /// 当前标识符的光照采用的是LightMap还是LightProbe
        /// </summary>
        [SerializeField]
        public List<bool> m_identifierUseLightMapOrProbe;

        [SerializeField]
        public float[] m_lightMapIndex;
        
        
        [SerializeField]
        public Vector4[] m_lightMapScaleOffset;
        
        //------------- 不存LightProbe ------------------------------------ 
        // [SerializeField]
        // public SphericalHarmonicsL2[] m_LightProbes;
 
        [SerializeField]
        private MaterialPropertyData[] m_matPropertyDatas;

        public int GetLocalToWorldMatrix4x4Count()
        {
            return m_localToWorlds.Length;
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
    }
}