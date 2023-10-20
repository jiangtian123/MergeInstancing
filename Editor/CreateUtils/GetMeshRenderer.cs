using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Unity.MergeInstancingSystem.CreateUtils
{
    public class GetMeshRenderer
    {
        private class MeshRendererCalculator
        {
            private bool m_isCalculated = false;
            /// <summary>
            /// 存储的是一组物体所包含的所有激活的MeshRenderer
            /// </summary>
            private List<MeshRenderer> m_meshRenderers = new List<MeshRenderer>();
            /// <summary>
            /// 存储的是一组物体所包含的所有激活的LODGroup
            /// </summary>
            private List<LODGroup> m_lodGroups = new List<LODGroup>();

            private List<MeshRenderer> m_resultMeshRenderers = new List<MeshRenderer>();

            public List<MeshRenderer> ResultMeshRenderers
            {
                get { return m_resultMeshRenderers; }
            }

            public MeshRendererCalculator(List<GameObject> targetGameObjects)
            {
                for (int oi = 0; oi < targetGameObjects.Count; ++oi)
                {
                    var target = targetGameObjects[oi];
                    m_meshRenderers.AddRange(target.GetComponentsInChildren<MeshRenderer>());
                    m_lodGroups.AddRange(target.GetComponentsInChildren<LODGroup>());
                }
                RemoveDisabled(m_lodGroups);
                RemoveDisabled(m_meshRenderers);
            }
            /// <summary>
            /// 将拥有HLODMeshSetter ，LodGroup和MeshRenderer中最后一个Lod级别的Rednerer存下来
            /// HLODMeshSetter ，LodGroup和MeshRenderer不存重复的
            /// </summary>
            /// <param name="minObjectSize"></param>
            /// <param name="level">一个元素距离最高层级的距离</param>
            public void Calculate(float minObjectSize, int level)
            {
                //如果已经计算过一次就返回
                if (m_isCalculated == true)
                    return;
                //将MeshRenderer里和LodGroup重复的去掉
                for (int gi = 0; gi < m_lodGroups.Count; ++gi)
                {
                    LODGroup lodGroup = m_lodGroups[gi];
                    LOD[] lods = lodGroup.GetLODs();
                    for (int li = 0; li < lods.Length; ++li)
                    {
                        Renderer[] lodRenderers = lods[li].renderers;

                        //Remove every mesh renderer which is registered to the LODGroup.
                        for (int ri = 0; ri < lodRenderers.Length; ++ri)
                        {
                            MeshRenderer mr = lodRenderers[ri] as MeshRenderer;
                            if (mr == null)
                                continue;

                            m_meshRenderers.Remove(mr);
                        }
                    }

                    AddReusltFromLODGroup(lodGroup, minObjectSize,level);
                }

                for (int mi = 0; mi < m_meshRenderers.Count; ++mi)
                {
                    MeshRenderer mr = m_meshRenderers[mi];

                    float max = Mathf.Max(mr.bounds.size.x, mr.bounds.size.y, mr.bounds.size.z);
                    if (max < minObjectSize)
                        continue;

                    m_resultMeshRenderers.Add(mr);
                }

                m_isCalculated = true;
            }

            public void CalculateLod0(float minObjectSize)
            {
                if (m_isCalculated == true)
                    return;
                //将MeshRenderer里和LodGroup重复的去掉
                for (int gi = 0; gi < m_lodGroups.Count; ++gi)
                {
                    LODGroup lodGroup = m_lodGroups[gi];
                    LOD[] lods = lodGroup.GetLODs();
                    for (int li = 0; li < lods.Length; ++li)
                    {
                        Renderer[] lodRenderers = lods[li].renderers;

                        //Remove every mesh renderer which is registered to the LODGroup.
                        for (int ri = 0; ri < lodRenderers.Length; ++ri)
                        {
                            MeshRenderer mr = lodRenderers[ri] as MeshRenderer;
                            if (mr == null)
                                continue;

                            m_meshRenderers.Remove(mr);
                        }
                    }

                    AddReusltFromLODGroup0(lodGroup, minObjectSize);
                }

                for (int mi = 0; mi < m_meshRenderers.Count; ++mi)
                {
                    MeshRenderer mr = m_meshRenderers[mi];

                    float max = Mathf.Max(mr.bounds.size.x, mr.bounds.size.y, mr.bounds.size.z);
                    if (max < minObjectSize)
                        continue;

                    m_resultMeshRenderers.Add(mr);
                }

                m_isCalculated = true;
            }
            /// <summary>
            /// 按层级拿lrenderer
            /// </summary>
            /// <param name="lodGroup"></param>
            /// <param name="minObjectSize"></param>
            private void AddReusltFromLODGroup(LODGroup lodGroup, float minObjectSize,int level)
            {
                LOD[] lods = lodGroup.GetLODs();

                int index = level > lods.Length - 1 ? lods.Length - 1 : level;

                Renderer[] renderers = lods[index].renderers;
                for (int ri = 0; ri < renderers.Length; ++ri)
                {
                    MeshRenderer mr = renderers[ri] as MeshRenderer;

                    if (mr == null)
                        continue;

                    if (mr.gameObject.activeInHierarchy == false || mr.enabled == false)
                        continue;

                    float max = Mathf.Max(mr.bounds.size.x, mr.bounds.size.y, mr.bounds.size.z);
                    if (max < minObjectSize)
                        continue;

                    m_resultMeshRenderers.Add(mr);
                }
            }

            private void AddReusltFromLODGroup0(LODGroup lodGroup, float minObjectSize)
            {
                LOD[] lods = lodGroup.GetLODs();
                Renderer[] renderers = lods[0].renderers;
                for (int ri = 0; ri < renderers.Length; ++ri)
                {
                    MeshRenderer mr = renderers[ri] as MeshRenderer;

                    if (mr == null)
                        continue;

                    if (mr.gameObject.activeInHierarchy == false || mr.enabled == false)
                        continue;

                    float max = Mathf.Max(mr.bounds.size.x, mr.bounds.size.y, mr.bounds.size.z);
                    if (max < minObjectSize)
                        continue;

                    m_resultMeshRenderers.Add(mr);
                }
            }
            /// <summary>
            /// 去掉失活的。
            /// </summary>
            /// <param name="componentList"></param>
            private void RemoveDisabled(List<LODGroup> componentList)
            {
                for (int i = 0; i < componentList.Count; ++i)
                {
                    if (componentList[i].enabled == true && componentList[i].gameObject.activeInHierarchy == true)
                    {
                        continue;
                    }

                    int backIndex = componentList.Count - 1;
                    componentList[i] = componentList[backIndex];
                    componentList.RemoveAt(backIndex);
                    i -= 1;
                }
            }

            private void RemoveDisabled(List<MeshRenderer> componentList)
            {
                for (int i = 0; i < componentList.Count; ++i)
                {
                    if (componentList[i].enabled == true && componentList[i].gameObject.activeInHierarchy == true)
                    {
                        continue;
                    }

                    int backIndex = componentList.Count - 1;
                    componentList[i] = componentList[backIndex];
                    componentList.RemoveAt(backIndex);
                    i -= 1;
                }
            }
        }
        /// <summary>
        /// 按照层级拿物体的Renderer
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="currentNodeBounds"></param>
        /// <param name="minObjectSize"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public static List<MeshRenderer> GetMeshRenderers(GameObject gameObject, float minObjectSize,int level)
        {
            List<GameObject> tmpList = new List<GameObject>();
            tmpList.Add(gameObject);

            MeshRendererCalculator calculator = new MeshRendererCalculator(tmpList);
            calculator.Calculate(minObjectSize, level);
            return calculator.ResultMeshRenderers;
        }
        /// <summary>
        /// 取一个物体下面的meshrender，但是只拿lod0级别的，如果没有lodgroup，就取所有的
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static List<MeshRenderer> GetMeshRenderers(GameObject gameObject, float minObjectSize)
        {
            List<GameObject> tmpList = new List<GameObject>();
            tmpList.Add(gameObject);
            MeshRendererCalculator calculator = new MeshRendererCalculator(tmpList);
            calculator.CalculateLod0(minObjectSize);
            return calculator.ResultMeshRenderers;
        }
    }
}