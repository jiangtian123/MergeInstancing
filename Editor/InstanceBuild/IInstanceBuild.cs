using System.Collections.Generic;
using Unity.MergeInstancingSystem.SpaceManager;
using System;
using UnityEngine;
using Unity.MergeInstancingSystem.Utils;

namespace Unity.MergeInstancingSystem.InstanceBuild
{
    public interface IInstanceBuild
    {
        public void Build(SpaceNode rootNode, GameObject root,Instance instance, Action<float> onProgress);
    }
}