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
        
        DGameObjectData gameObjectData = new DGameObjectData();
        
        
        UnityEngine.Profiling.CustomSampler Circulation = CustomSampler.Create("For Circulation");
        UnityEngine.Profiling.CustomSampler CallFun = CustomSampler.Create("Sub Sector Call");
        /// <summary>
        /// 根据Lod级别准备需要渲染的数据
        /// </summary>
        /// <param name="lodLevel"></param>
        /// <param name="gameObjectData"></param>
        
        public void DispatchSetup(int lodLevel,SerializableData serializableData,InstanceSubSector[] instanceSubSectors,bool isShadow)
        {
            //Profiler.BeginSample("DispatchSetup");
            if (lodLevel >= m_lod.Length)
            {
                Debug.Log("has error on DispatchSetup");
                //Profiler.EndSample();
                return;
            }
            if (lodLevel == -1)
            {
                //Profiler.EndSample();
                return;
            }
            var sector = m_lod[lodLevel];
            var lodSerializable = serializableData.m_LodData[lodLevel];
            int number = 0;
            Circulation.Begin();
            for (int i = 0; i < sector.MeshCount; i++)
            {
                var subsectorIndex = sector.m_meshs[i];
                var subsector = instanceSubSectors[subsectorIndex];
                gameObjectData.m_MatrixIndex = i;
                if (!isShadow && subsector.useLightMap)
                {
                    gameObjectData.m_LightIndex = number;
                    number++;
                }
                //CallFun.Begin();
                subsector.AddData(ref gameObjectData,ref lodSerializable,isShadow);
                //CallFun.End();
            }
            Circulation.End();
            //Profiler.EndSample();
        }
    }
}