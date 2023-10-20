using System.Collections.Generic;
using Unity.MergeInstancingSystem.SpaceManager;
using System;
using UnityEngine;
using Unity.MergeInstancingSystem.Utils;

namespace Unity.MergeInstancingSystem.InstanceBuild
{
    public interface IInstanceBuild
    {
       /// <summary>
       /// 
       /// </summary>
       /// <param name="rootNode"></param>
       /// <param name="info"></param>
       /// <param name="instanceData"></param>
       /// <param name="root"></param>
       /// <param name="cullDistance"></param>
       /// <param name="lodDistance"></param>
       /// <param name="writeNoPrefab"></param>
       /// <param name="extractMaterial"></param>
       /// <param name="onProgress"></param>
        public void Build(SpaceNode rootNode,List<InstanceBuildInfo> 
            info,AllInstanceData instanceData, GameObject root,float cullDistance, float lodDistance, bool writeNoPrefab,bool useMotionvector ,bool extractMaterial, Action<float> onProgress);
    }
}