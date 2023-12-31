﻿using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.MergeInstancingSystem.CreateUtils;
using Unity.MergeInstancingSystem.Utils;
using UnityEditor;
using UnityEngine;
namespace Unity.MergeInstancingSystem.SpaceManager
{
    public class SpaceNode
    {
        
        /// <summary>
        /// 新建一个Node，只包含一个包围盒
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static SpaceNode CreateSpaceNodeWithBounds(Bounds bounds)
        {
            var spaceNode = new SpaceNode();
            spaceNode.Bounds = bounds;
            return spaceNode;
        }
        
        
        /// <summary>
        /// 当前节点的包围盒
        /// </summary>
        private Bounds m_bounds;
        
        
        /// <summary>
        /// 父节点
        /// </summary>
        private SpaceNode m_parentNode;
        
        
        /// <summary>
        /// 孩子节点
        /// </summary>
        private List<SpaceNode> m_childTreeNodes = new List<SpaceNode>(); 
        
        
        /// <summary>
        /// 当前节点包含的物体
        /// </summary>
        private List<GameObject> m_objcets = new List<GameObject>();
        
        public Bounds Bounds
        {
            set { m_bounds = value;}
            get { return m_bounds; }
        }
        /// <summary>
        /// 取Lod0 级别的Obj 分类
        /// </summary>
        public Dictionary<long, NodeObject> classificationObjects = new Dictionary<long, NodeObject>();

        public List<GameObject> Objects
        {
            get { return m_objcets; }
        }
        
        
        /// <summary>
        /// 每次设置父节点时，会把自己加到父节点的子队列去
        /// </summary>
        public SpaceNode ParentNode
        {
            set
            {
                if (m_parentNode != null)
                    m_parentNode.m_childTreeNodes.Remove(this);
                m_parentNode = value;
                if (value != null)
                    value.m_childTreeNodes.Add(this);
            }
            get { return m_parentNode; }
        }
        public SpaceNode GetChild(int index)
        {
            return m_childTreeNodes[index];
        }
        public int GetChildCount()
        {
            return m_childTreeNodes.Count;
        }
        /// <summary>
        /// 如果子节点数量大于0，则有孩子节点
        /// </summary>
        /// <returns></returns>
        public bool HasChild()
        {
            return m_childTreeNodes.Count > 0;
        }
        /// <summary>
        /// 后续遍历四叉树，然后转成List
        /// </summary>
        /// <param name="nodeList"></param>
        
        public  List<SpaceNode> PostOrderTraversalConverList()
        {
            List<SpaceNode> nodeList = new List<SpaceNode>();
            // 遍历所有子节点
            foreach (var child in m_childTreeNodes) {
                nodeList.AddRange(child.PostOrderTraversalConverList()); // 递归调用子节点的后序遍历
            }
            //处理节点
            nodeList.Add(this);
            return nodeList;
        }
        /// <summary>
        /// 对Lod0级别的Obj进行按照Mesh和Mat分类
        /// </summary>
        public void ClassificationTree()
        {
            foreach (var objcet in m_objcets)
            {
                //拿到的是所有Lod0级别的MeshRender
                var Lod0Obj = GetMeshRenderer.GetMeshRenderers(objcet,0.01f);
                //将Lod0级别的Meshrender转换成NodeObject
                foreach (var meshRenderer in Lod0Obj)
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
            }

            foreach (var spaceNode in m_childTreeNodes)
            {
                spaceNode.ClassificationTree();
            }
        }

        public Bounds CalculateRealBound()
        {
            Bounds ret = new Bounds();
            ret.center = Vector3.zero;
            ret.size = Vector3.zero;
            if (m_objcets.Count == 0)
            {
                return ret;
            }
            var objBound = ObjectUtils.GetObjBounds(m_objcets[0]);
            for (int i = 1; i < m_objcets.Count; i++)
            {
                var bound = ObjectUtils.GetObjBounds(m_objcets[i]);
                objBound.Encapsulate(bound);
            }
            ret.center = objBound.center;
            ret.size = objBound.size;
            return ret;
        }
    }
}