using System;
using System.Collections.Generic;
using Unity.MergeInstancingSystem.SpaceManager;
using Unity.MergeInstancingSystem.Utils;
using Unity.Collections;
using UnityEditor;
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
        /// <summary>
        /// 一种mesh加mat组合的map
        /// </summary>
        public Dictionary<long, NodeObject> classificationObjects = new Dictionary<long, NodeObject>();
        /// <summary>
        /// 将MeshRenderer转换成InstanceBuildInfo，这个MeshRenderer是Lod里面的
        /// </summary>
        /// <param name="meshRenderer">一个MeshRenderer可能有SubMesh</param>
        public void AddWorkingObject(MeshRenderer meshRenderer)
        {
            var mats = meshRenderer.sharedMaterials;
            var mesh = meshRenderer.gameObject.GetComponent<MeshFilter>().sharedMesh;
            var light_mapindex = meshRenderer.lightmapIndex;
            LightMode tempLightMode =  (light_mapindex >=0 && light_mapindex < LightmapSettings.lightmaps.Length) ? LightMode.LightMap : LightMode.LightProbe;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                long inde = long.Parse($"{mesh.GetHashCode()}{mat.GetHashCode()}");
                if (classificationObjects.TryGetValue(inde,out var nodeObject))
                {
                    MinGameObject temMinGameObj = new MinGameObject(meshRenderer,i,tempLightMode == LightMode.LightMap);
                    if (tempLightMode != nodeObject.m_lightMode)
                    {
                        EditorUtility.DisplayDialog("警告", $"有OBJ的光照模型与所属类不同，请检查后重新设置,OBJ使用的是{meshRenderer.gameObject.name}", "确定");
                    }
                    nodeObject.AddMinGameObj(temMinGameObj);
                }
                else
                {
                    NodeObject temNodeobj = new NodeObject(i,mesh,mat,inde,meshRenderer as  Renderer);
                    MinGameObject temMinGameObj = new MinGameObject(meshRenderer,i,tempLightMode == LightMode.LightMap);
                    temNodeobj.AddMinGameObj(temMinGameObj);
                    classificationObjects.Add(inde,temNodeobj);
                }
            }

        }
        public List<int> Distances = new List<int>();
    }
}