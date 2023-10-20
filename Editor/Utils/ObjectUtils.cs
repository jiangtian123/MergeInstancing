using System.Collections.Generic;
using System.Linq;
using Unity.MergeInstancingSystem.SpaceManager;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace Unity.MergeInstancingSystem.Utils
{
    public static class ObjectUtils
    {
        /// <summary>
        /// 按照树的遍历方式拿到每个有所需T类型的Component
        /// </summary>
        /// <param name="root"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<T> GetComponentsInChildren<T>(GameObject root) where T : Component
        {
            LinkedList<T> result = new LinkedList<T>();
            Queue<GameObject> queue = new Queue<GameObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                GameObject go = queue.Dequeue();
                T component = go.GetComponent<T>();
                if (component != null)
                    result.AddFirst(component);

                foreach (Transform child in go.transform)
                {
                    queue.Enqueue(child.gameObject);
                }
            }

            return result.ToList();
        }
         /// <summary>
        /// 返回一个Instance下的所有实例
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        public static List<GameObject> InstanceTargets(GameObject root)
        {
            //
            List<GameObject> targets = new List<GameObject>();
            //获取所有的LODGroup组件
            List<LODGroup> lodGroups = GetComponentsInChildren<LODGroup>(root);
            //获取所有的MeshRenderer组件
            List<MeshRenderer> meshRenderers = GetComponentsInChildren<MeshRenderer>(root);
            //移除所有带LodGroup的MeshRenderer
            for (int i = 0; i < lodGroups.Count; ++i)
            {
                if ( lodGroups[i].enabled == false )
                    continue;
                if (lodGroups[i].gameObject.activeInHierarchy == false)
                    continue;

                targets.Add(lodGroups[i].gameObject);
                //会从所有meshRenderers中移除带LodGroup的物体
                meshRenderers.RemoveAll(lodGroups[i].GetComponentsInChildren<MeshRenderer>());
            }
            for (int ri = 0; ri < meshRenderers.Count; ++ri)
            {
                if (meshRenderers[ri].enabled == false)
                    continue;
                if (meshRenderers[ri].gameObject.activeInHierarchy == false)
                    continue;

                targets.Add(meshRenderers[ri].gameObject);
            }
            
            HashSet<GameObject> targetsByPrefab = new HashSet<GameObject>();
            for (int ti = 0; ti < targets.Count; ++ti)
            {
                var targetPrefab = GetCandidatePrefabRoot(root, targets[ti]);
                targetsByPrefab.Add(targetPrefab);
            }

            return targetsByPrefab.ToList();
        }
         /// <summary>
         /// 找一个预制体的根节点
         /// </summary>
         /// <param name="hlodRoot">Holds根节点，找到的根节点不能等于它</param>
         /// <param name="target">目标物体</param>
         /// <returns>返回的是一个预制体的根节点，如果这个物体不是预制体，返回的是它自己</returns>
         public static GameObject GetCandidatePrefabRoot(GameObject hlodRoot, GameObject target)
         {
             //如果不是预制体则直接返回
             if (PrefabUtility.IsPartOfAnyPrefab(target) == false)
                 return target;

             GameObject candidate = target;
             //最外层预制体实例的根
             GameObject outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(target);

             while (Equals(target,outermost) == false && 
                    Equals(GetParent(target), hlodRoot) == false)    //< HLOD root should not be included.
             {
                 target = GetParent(target);
                 if (PrefabUtility.IsAnyPrefabInstanceRoot(target))
                 {
                     candidate = target;
                 }
             }

             return candidate;
         }

         public static string GetAssetGuid(Object obj)
         {
             string assetpath = AssetDatabase.GetAssetPath(obj);
             GUID assetGuid = AssetDatabase.GUIDFromAssetPath(assetpath);
             return assetGuid.ToString();
         }
         private static GameObject GetParent(GameObject go)
         {
             return go.transform.parent.gameObject;
         }
         
         public static AllInstanceData GetInstanceData(SpaceNode rootNode)
         {
             AllInstanceData result = new AllInstanceData();
             //将树按照后续遍历展开
             var postTreeList = rootNode.PostOrderTraversalConverList();
             //每个 i 是一个 树的节点
             for (int i = 0; i < postTreeList.Count; i++)
             {
                 Dictionary<string, List<NodeObject>> classificationObjects = new Dictionary<string, List<NodeObject>>();
                 //一个节点的所有obj,先把树的节点里面的OBJ分类
                 foreach (var node in postTreeList[i].Objects)
                 {
                     //会将一个Obj的所有Meshrender拿出来转换成NodeObject，不区分Lod
                     var nodeObjects = node.ToNodeObject();
                     foreach (var nodeObject in nodeObjects)
                     {
                         if (classificationObjects.TryGetValue(nodeObject.Identifier,out var c_nodeObjects))
                         {
                             c_nodeObjects.Add(nodeObject);
                         }
                         else
                         {
                             List<NodeObject> tempList = new List<NodeObject>();
                             tempList.Add(nodeObject);
                             classificationObjects.Add(nodeObject.Identifier,tempList);
                         }
                     }
                 }
                 //按照mesh 和 mat 分类好的Node 存数据
                 foreach (var meshList in classificationObjects)
                 {
                     //这些所有的NodeObj是共享mesh和材质的
                     foreach (var node in meshList.Value)
                     {
                         result.AddItem(node.m_mesh);
                         var meshRenderer = node.m_renderer;
                         var light_mapindex = meshRenderer.lightmapIndex;
                         bool m_NeedLightMap = (light_mapindex >=0 && light_mapindex < LightmapSettings.lightmaps.Length) ? true : false;
                         result.AddItem(node.m_material);
                         int Identifier = node.m_mesh.GetHashCode() + node.m_material.GetHashCode();
                         result.CalculatorIdentifier(Identifier, m_NeedLightMap);
                         result.AddItem(meshRenderer as Renderer);
                     }
                 }
                 classificationObjects.Clear();
             }
             
             return result;
         }
         public static Bounds GetObjBounds(GameObject gameObject)
         {
             Bounds ret = new Bounds();
             var renderers = gameObject.GetComponentsInChildren<Renderer>();
             if (renderers.Length == 0)
             {
                 ret.center = Vector3.zero;
                 ret.size = Vector3.zero;
                 return ret;
             }
             Bounds bounds = renderers[0].bounds;
             //扩展包围盒使其包含所有的OBJ
             for (int i = 1; i < renderers.Length; ++i)
             {
                 bounds.Encapsulate(renderers[i].bounds);
             }

             ret.center = bounds.center;
             ret.size = bounds.size;
             return ret;
         }
    }
}