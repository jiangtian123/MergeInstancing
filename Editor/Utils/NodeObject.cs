using System.Collections.Generic;
using Unity.MergeInstancingSystem.Render;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.Utils
{
    public enum LightMode
    {
        LightMap,
        LightProbe
    }
    /// <summary>
    /// 同一种Mesh和材质组成，有共同的光照模型
    /// </summary>
    public class NodeObject
    {
        /// <summary>
        /// 这个数据最后会变成 head 加 length的格式
        /// </summary>
        public List<MinGameObject> m_gameobjs;
        public long Identifier;
        public Mesh m_mesh;
        public Material m_mat;
        public LightMode m_lightMode;
        public RendererQueue m_queue;
        public int subMeshIndex;
        public bool m_castShadow;
        public NodeObject(int subMesh,Mesh mesh,Material mat,long identifier,Renderer renderer)
        {
            var light_mapindex = renderer.lightmapIndex;
            subMeshIndex = subMesh;
            m_castShadow = renderer.shadowCastingMode == ShadowCastingMode.Off ? false : true;
            m_lightMode = (light_mapindex >=0 && light_mapindex < LightmapSettings.lightmaps.Length) ? LightMode.LightMap : LightMode.LightProbe;
            m_queue = mat.renderQueue > 2500 ? RendererQueue.Transparent : RendererQueue.Opaque;
            m_mat = mat;
            m_mesh = mesh;
            Identifier = identifier;
            m_gameobjs = new List<MinGameObject>();
        }

        public void AddMinGameObj(MinGameObject gameObject)
        {
            m_gameobjs.Add(gameObject);
        }
    }
}