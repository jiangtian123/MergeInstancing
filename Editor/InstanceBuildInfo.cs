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
        public Bounds CalculateRealBound()
        {
            Bounds ret = new Bounds();
            ret.center = Vector3.zero;
            ret.size = Vector3.zero;
            if (classificationObjects.Count == 0)
            {
                return ret;
            }

            List<Bounds> tempBoxs = new List<Bounds>();
            foreach (var nObjectsValue in classificationObjects.Values)
            {
                var mesh = nObjectsValue.m_mesh;
                var localBox = mesh.bounds;
                var worldBox = BoundsUtils.CalcLocalBounds(localBox,nObjectsValue.m_gameobjs[0].m_localtoworld);
                for (int i = 1; i < nObjectsValue.m_gameobjs.Count; i++)
                {
                    worldBox.Encapsulate(BoundsUtils.CalcLocalBounds(localBox,
                        nObjectsValue.m_gameobjs[i].m_localtoworld));
                }
                tempBoxs.Add(worldBox);
            }

            var objBound = tempBoxs[0];
            for (int i = 1; i < tempBoxs.Count; i++)
            {
                objBound.Encapsulate(tempBoxs[i]);
            }
            ret.center = objBound.center;
            ret.size = objBound.size;
            return ret;
        }
        Bounds TransformBoundsToWorldBounds(UnityEngine.Matrix4x4 Worldmatrix, Bounds localBounds)
        {
            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            Matrix4x4 matrix = Worldmatrix;

            Vector3[] points = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z),
            };

            for (int i = 0; i < points.Length; ++i)
            {
                points[i] = matrix.MultiplyPoint(points[i]);
            }

            Vector3 newMin = points[0];
            Vector3 newMax = points[0];

            for (int i = 1; i < points.Length; ++i)
            {
                if (newMin.x > points[i].x) newMin.x = points[i].x;
                if (newMax.x < points[i].x) newMax.x = points[i].x;
                
                if (newMin.y > points[i].y) newMin.y = points[i].y;
                if (newMax.y < points[i].y) newMax.y = points[i].y;
                
                if (newMin.z > points[i].z) newMin.z = points[i].z;
                if (newMax.z < points[i].z) newMax.z = points[i].z;
            }
            Bounds newBounds = new Bounds();
            newBounds.SetMinMax(newMin, newMax);
            return newBounds;
        }
    }
}