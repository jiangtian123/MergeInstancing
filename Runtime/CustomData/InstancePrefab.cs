using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.MergeInstancingSystem
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

#if UNITY_EDITOR
        public void GenerateBais()
        {
            int matirxNumber = 0;
            int lightNumber = 0;
            for (int i = 0; i < m_lod.Length; i++)
            {
                var serctor = m_lod[i];
                serctor.matrixBais = matirxNumber;
                matirxNumber += serctor.m_subSectors.Length;
                serctor.lightBais = lightNumber;
                for (int j = 0; j < serctor.m_subSectors.Length; j++)
                {
                    var subsector = serctor.m_subSectors[j];
                    if (subsector.useLightMap)
                    {
                        lightNumber++;
                    }
                }
            }
        }
#endif
        /// <summary>
        /// 
        /// </summary>
        /// <param name="instanceData"></param>
        /// <param name="objMatrixIndex">当前Gameobject的矩阵数据在数组中的起始位置</param>
        /// <param name="lightDataHead">当前Gameobject的灯光数据在数组中的起始位置</param>
        /// <param name="lodLevel">GameObject渲染时使用的Lod级别</param>
        /// <param name="instanceSubSectors"></param>
        /// <param name="isShadow"></param>
        public void DispatchSetup(InstanceData instanceData, int objMatrixIndex,int lightDataHead,int lodLevel,bool isShadow)
        {
            if (lodLevel == -1)
            {
                return;
            }
            var sector = m_lod[lodLevel];
            int number = 0;
            //提交这个Lod下的所有mesh
            for (int i = 0; i < sector.MeshCount; i++)
            {
                var subsector = sector.m_subSectors[i];
                var matrixOffest = sector.matrixBais;
                var lightOffest = sector.lightBais;
                int lightDataIndex = -1;
                int lightMapIndex = -1;
                if (!isShadow && subsector.useLightMap)
                {
                    lightDataIndex = lightDataHead + lightOffest + number++;
                    lightMapIndex = instanceData.m_lightMapIndex[lightDataIndex];
                }
                Vector4 index = new Vector4(objMatrixIndex + matrixOffest + i, lightDataIndex, lightMapIndex, 0);
                subsector.AddData(index);
            }
        }
    }
}