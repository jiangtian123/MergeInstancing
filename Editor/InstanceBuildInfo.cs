using System;
using System.Collections.Generic;
using Unity.MergeInstancingSystem.SpaceManager;
using Unity.MergeInstancingSystem.Utils;
using Unity.Collections;
using UnityEngine;
namespace Unity.MergeInstancingSystem
{
    /// <summary>
    /// 每个节点保存的信息
    /// </summary>
    public class InstanceBuildInfo
    {
        public string Name = "";
        //TODO: remove this.
        public int ParentIndex = -1;
        public SpaceNode Target;
        
        public Dictionary<string, List<NodeObject>> classificationObjects = new Dictionary<string, List<NodeObject>>();

        public void AddWorkingObject(MeshRenderer meshRenderer)
        {
            var objcet = meshRenderer.gameObject;
            var cos = objcet.ToNodeObject();
            foreach (var co in cos)
            {
                if (classificationObjects.TryGetValue(co.Identifier, out var nodeObject))
                {
                    nodeObject.Add(co);
                }
                else
                {
                    List<NodeObject> tempList = new List<NodeObject>();
                    tempList.Add(co);
                    classificationObjects.Add(co.Identifier,tempList);
                }
            }
        }
    
        public List<int> Distances = new List<int>();
        

    }
}