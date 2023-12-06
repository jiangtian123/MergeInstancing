using System;
using System.Collections.Generic;
using Unity.MergeInstancingSystem.CustomData;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.MergeInstancingSystem.New
{
    /// <summary>
    /// 类似于预制体的结构
    /// </summary>
    [Serializable]
    public class InstancePrefab : ScriptableObject
    {
        /// <summary>
        /// 一个预制体有多个Lod
        /// </summary>
        [SerializeField]
        public InstanceSector[] m_lod;
        /// <summary>
        /// 预制体做剔除用的包围盒,就是Mesh的包围盒
        /// </summary>
        [SerializeField]
        public DAABB m_box;
        /// <summary>
        /// 每种Lod的占屏幕大小
        /// </summary>
        [SerializeField]
        public List<float> m_LODInfos;

        /// <summary>
        /// 根据Lod级别准备需要渲染的数据
        /// </summary>
        /// <param name="lodLevel"></param>
        /// <param name="gameObjectData"></param>
        private DGameObjectData gameObjectData = new DGameObjectData();
        public void DispatchSetup(int  objMatrixIndex,int lightDataHead,int lodLevel,InstanceSubSector[] instanceSubSectors,bool isShadow)
        {
            if (lodLevel >= m_lod.Length)
            {
                Debug.Log("has error on DispatchSetup");
                return;
            }
            if (lodLevel == -1)
            {
                return;
            }
            var sector = m_lod[lodLevel];
            int number = 0;
            for (int i = 0; i < sector.MeshCount; i++)
            {
                
                var subsectorIndex = sector.m_meshs[i];
                var meshMatrixindex = sector.m_prefabMatrix[i];
                var subsector = instanceSubSectors[subsectorIndex];
                int lightDataIndex = -1;
                if (!isShadow && subsector.useLightMap)
                {
                    lightDataIndex = lightDataHead + number;
                }
                GPUIndex index = new GPUIndex(objMatrixIndex,meshMatrixindex,lightDataIndex);
                subsector.AddData(index);
            }
        }
    }
}