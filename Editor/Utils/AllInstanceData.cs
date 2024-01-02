using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.Utils
{
    public class AllInstanceData
    {
        #region variables

        private List<Mesh> m_meshs = new List<Mesh>();
        private List<Material> m_materials = new List<Material>();
        private List<MinGameObject> m_minGameObjects = new List<MinGameObject>();

        #endregion

        /// <summary>
        /// 以最小的单位收集
        /// </summary>
        /// <param name="nodeObject"></param>
        public void AddNodeObject(NodeObject nodeObject, bool useLightMap)
        {
            var mesh = nodeObject.m_mesh;
            m_meshs.AddLikeHashSet(mesh);
            var mat = nodeObject.m_mat;
            m_materials.AddLikeHashSet(mat);
            var identifier = nodeObject.Identifier;
            AddDataWithLightMap(nodeObject);
        }

        private void AddDataWithLightMap(NodeObject nodeObject)
        {
            foreach (var minGameObject in nodeObject.m_gameobjs)
            {
                m_minGameObjects.AddLikeHashSet(minGameObject);
            }
        }

        #region GetAsset
        public int GetAssetIndex(Mesh mesh)
        {
            return m_meshs.IndexOf(mesh);
        }
        public int GetAssetIndex(Material material)
        {
            material.enableInstancing = true;
            return m_materials.IndexOf(material);
        }
        public int GetAssetIndex(MinGameObject minObj)
        {
            return m_minGameObjects.IndexOf(minObj);
        }

        public List<Mesh> Meshes
        {
            get
            {
                return m_meshs;
            }
        }
        public List<Material> Materials
        {
            get
            {
                return m_materials;
            }
        }

        public List<DTransform> GetMatrix4X4s()
        {
            List<DTransform> temp = new List<DTransform>();
            for (int i = 0; i < m_minGameObjects.Count; i++)
            {
                temp.Add(m_minGameObjects[i].m_localtoworld);
            }
            
            return temp;
        }
        public float[] GetLightMapIndexs()
        {
            List<float> temp = new List<float>();
            for (int i = 0; i < m_minGameObjects.Count; i++)
            {
                if (m_minGameObjects[i].m_lightMapIndex == null)
                {
                    break;
                }
                temp.Add((float)m_minGameObjects[i].m_lightMapIndex);
            }

            return temp.ToArray();
        }
        public Vector4[] GetLigthMapOffests()
        {
            List<Vector4> temp = new List<Vector4>();
            for (int i = 0; i < m_minGameObjects.Count; i++)
            {
                if (m_minGameObjects[i].m_lightMapOffest == null)
                {
                    break;
                }
                temp.Add((Vector4)m_minGameObjects[i].m_lightMapOffest);
            }
            return temp.ToArray();
        }
        #endregion
        
    }
}