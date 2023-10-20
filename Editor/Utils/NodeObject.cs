using System.Collections.Generic;
using Unity.MergeInstancingSystem.Render;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.Utils
{
    public static class GameObjectExtension
    {
        public static List<NodeObject> ToNodeObject(this GameObject obj)
        {
            List<NodeObject> result = new List<NodeObject>();
            var meshRenderers = obj.GetComponentsInChildren<MeshRenderer>();
            foreach (var meshRenderer in meshRenderers)
            {
                var renderer = meshRenderer as Renderer;
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    NodeObject tempNode = new NodeObject();
                    tempNode.FromGameObject(renderer,i,mats[i]);
                    result.Add(tempNode);
                }
            }
            
            return result;
        }
    }
    /// <summary>
    /// 按照SubMesh和材质分组
    /// </summary>
    public class NodeObject
    {
        public string Identifier
        {
            get
            {
                return $"{m_meshGUID}+{m_matGUID}";
            }
        }
        public string m_matGUID;

        public string m_meshGUID;

        public int m_subMeshIndex;
        public bool m_NeedLightMap;
        public Mesh m_mesh;
        public bool m_castShadow;
        public RendererQueue m_queue;
        public Material m_material;
        public Renderer m_renderer;

        public void FromGameObject(Renderer renderer,int subMeshIndex,Material material)
        {
            m_mesh = (renderer as MeshRenderer)?.GetComponent<MeshFilter>().sharedMesh;
            m_subMeshIndex = subMeshIndex;
            m_material = material;
            m_matGUID = material.GetHashCode().ToString();//ObjectUtils.GetAssetGuid(material);
            m_meshGUID = m_mesh.GetHashCode().ToString();// ObjectUtils.GetAssetGuid(m_mesh);
            m_renderer = renderer;
            m_castShadow = renderer.shadowCastingMode == ShadowCastingMode.On;
            m_queue = m_material.renderQueue < 3000 ? RendererQueue.Opaque : RendererQueue.Transparent;
            var light_mapindex = renderer.lightmapIndex;
            m_NeedLightMap = (light_mapindex >=0 && light_mapindex < LightmapSettings.lightmaps.Length) ? true : false;
        }
    }
}