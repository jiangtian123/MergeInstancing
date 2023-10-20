using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.Utils
{
    public class AllInstanceData
    {
        public enum AssetsEnum
        {
            mesh,
            material,
            matrix4x4,
            LightMapIndex,
            LightScaleOffset,
            SH,
        }
        public List<Mesh> m_meshs = new List<Mesh>();
        /// <summary>
        /// mesh hashCode 对应的mesh在list中的位置
        /// </summary>
        public Dictionary<string, int> m_meshesMap = new Dictionary<string, int>();

        public List<Material> m_materials = new List<Material>();

        public List<int> m_matCitationNumber = new List<int>();
        
        /// <summary>
        /// 每种Mesh+Mat组成的标识符，用来提交
        /// </summary>
        public List<int> m_matAndMeshIdentifiers = new List<int>();
        
        /// <summary>
        /// 每种标识符有多少种引用，用来初始化分配内存
        /// </summary>
        public List<int> m_identifierCounts = new List<int>();

        /// <summary>
        /// 每种标识符使用的是LightMap还是LightProbe
        /// </summary>

        public List<bool> m_identifierUseLightMapOrProbe = new List<bool>();

        /// <summary>
        /// 材质ID对应的材质在list中的位置
        /// </summary>
        public Dictionary<string, int> m_materialsMap = new Dictionary<string, int>();

        
        /// <summary>
        /// 所有的矩阵
        /// </summary>
        public List<Matrix4x4> m_matrix4x4 = new List<Matrix4x4>();
        
        /// <summary>
        /// Renderer 对应的矩阵的在list中的位置
        /// </summary>
        public Dictionary<string, int> m_rendererMapMatrix4x4 = new Dictionary<string, int>();
        /// <summary>
        /// 每个Render都有一个对应的值
        /// </summary>
        public List<float> m_lightMapIndex = new List<float>();
        public Dictionary<string, int> m_RenderMaplightMap = new Dictionary<string, int>();

        public List<Vector4> m_lightMapScaleOffset = new List<Vector4>();
        public Dictionary<string, int> m_RenderMaplightMapScaleOffset = new Dictionary<string, int>();
        //----------------- 不存LightProbe ---------------------------------------------------------------------
        // public List<SphericalHarmonicsL2> m_LightProbes = new List<SphericalHarmonicsL2>();
        // public Dictionary<string, int> m_RenderMapLightProbes = new Dictionary<string, int>();
        public int GetAssetsPositon(string Guid ,AssetsEnum assetsType)
        {
            switch (assetsType) 
            {
                case AssetsEnum.mesh:
                {
                    if (m_meshesMap.TryGetValue(Guid,out var index))
                    {
                        return index;
                    }
                    else
                    {
                        return 0;
                    }
                }
                case AssetsEnum.material:
                {
                    if (m_materialsMap.TryGetValue(Guid,out var index))
                    {
                        return index;
                    }
                    else
                    {
                        return 0;
                    }
                }
                case AssetsEnum.matrix4x4:
                {
                    if (m_rendererMapMatrix4x4.TryGetValue(Guid,out var index))
                    {
                        return index;
                    }
                    else
                    {
                        return 0;
                    }
                }
                case AssetsEnum.LightMapIndex:
                {
                    if (m_RenderMaplightMap.TryGetValue(Guid,out var index))
                    {
                        return index;
                    }
                    else
                    {
                        return 0;
                    }
                }
                case AssetsEnum.LightScaleOffset:
                {
                    if (m_RenderMaplightMapScaleOffset.TryGetValue(Guid,out var index))
                    {
                        return index;
                    }
                    else
                    {
                        return 0;
                    }
                }
                // case AssetsEnum.SH:
                // {
                //     if (m_RenderMapLightProbes.TryGetValue(Guid,out var index))
                //     {
                //         return index;
                //     }
                //     else
                //     {
                //         return -1;
                //     }
                // }
                default:
                    return 0;
            }
        }
        
        public void AddItem(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            string meshHashCode = mesh.GetHashCode().ToString();
            if (!m_meshesMap.ContainsKey(meshHashCode))
            {
                m_meshs.Add(mesh);
                int index = m_meshs.Count - 1;
                m_meshesMap.Add(meshHashCode,index);
            }
        }

        public void AddItem(Material item)
        {
            if (item == null)
            {
                return;
            }

            item.enableInstancing = true;
            string matHashCode = item.GetHashCode().ToString();
            if (!m_materialsMap.TryGetValue(matHashCode,out var position))
            {
                m_matCitationNumber.Add(1);
                m_materials.Add(item);
                int index = m_materials.Count - 1;
                m_materialsMap.Add(matHashCode,index);
            }
            else
            {
                int count = m_matCitationNumber[position];
                count++;
                m_matCitationNumber[position] = count;
            }
        }
        /// <summary>
        /// 添加矩阵和光照信息
        /// </summary>
        /// <param name="renderer"></param>
        public void AddItem(Renderer renderer)
        {
            string rendererHashCode = renderer.GetHashCode().ToString();
            if (renderer == null)
            {
                return;
            }
            //----------------- 矩阵 ------------------------------------------------------
            if ( !m_rendererMapMatrix4x4.ContainsKey(rendererHashCode))
            {
                var matrix4x4 = renderer.gameObject.transform.localToWorldMatrix;
                m_matrix4x4.Add(matrix4x4);
                int matrix4x4_index = m_matrix4x4.Count - 1;
                m_rendererMapMatrix4x4.Add(rendererHashCode,matrix4x4_index);
            }
            //----------------- lightmap and shadow mask and offest--------------------------------------------------
            var light_mapindex = renderer.lightmapIndex;
            if (light_mapindex >=0 && light_mapindex < LightmapSettings.lightmaps.Length&&!m_RenderMaplightMap.ContainsKey(rendererHashCode))
            {
                m_lightMapIndex.Add((float)light_mapindex);
                int index = m_lightMapIndex.Count - 1;
                m_RenderMaplightMap.Add(rendererHashCode, index);
                // ---------------- lightMapOffset -----------------------------------------------------------
                var lightMapScaleOffset = renderer.lightmapScaleOffset;
                m_lightMapScaleOffset.Add(lightMapScaleOffset);
                int lightMapScaleOffset_index = m_matrix4x4.Count - 1;
                m_RenderMaplightMapScaleOffset.Add(rendererHashCode, lightMapScaleOffset_index);
                //------------------ 如果有Light就不计算Proble -------------------------------------------
                return;
            }
            //----------------- light probe -------------------------------------------------------------
            // if (!m_RenderMapLightProbes.ContainsKey(rendererHashCode))
            // {
            //     var position = renderer.transform.position;
            //     SphericalHarmonicsL2 sh = new SphericalHarmonicsL2();
            //     LightProbes.GetInterpolatedProbe(position,null,out sh);
            //     m_LightProbes.Add(sh);
            //     int lightProbesIndex = m_LightProbes.Count - 1;
            //     m_RenderMapLightProbes.Add(rendererHashCode,lightProbesIndex);
            // }
        }
        
        public void CalculatorIdentifier(int Identifier,bool useLightMap)
        {
            int index = m_matAndMeshIdentifiers.IndexOf(Identifier);
            if ( index != -1)
            {
                int count = m_identifierCounts[index];
                count += 1;
                m_identifierCounts[index] = count;
            }
            else
            {
                m_matAndMeshIdentifiers.Add(Identifier);
                m_identifierUseLightMapOrProbe.Add(useLightMap);
                m_identifierCounts.Add(1);
            }
        }

        public bool GetIdentifierLightMode(int identifier)
        {
            int index = m_matAndMeshIdentifiers.IndexOf(identifier);
            if (index != -1)
            {
                return m_identifierUseLightMapOrProbe[index];
            }
            else
            {
                return false;
            }
        }

        public Material GetMat(int index)
        {
           return m_materials[index];
        }
        public Mesh GetMesh(int index)
        {
            return m_meshs[index];
        }
        //------------------ 将 ID 改为 INT，用 String做比对太耗时了 ---------------------------------------
        public int GetIdentifier(int meshID,int matID)
        {
            Mesh mesh = m_meshs[meshID];
            Material material = m_materials[matID];
            int meshGuid = mesh.GetHashCode();
            int matGuid = material.GetHashCode();
            return meshGuid + matGuid;
        }
    }
}