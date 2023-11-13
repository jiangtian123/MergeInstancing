using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.MergeInstancingSystem.CreateUtils;
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
             List<NodeObject> useLightMapNode = new List<NodeObject>();
             List<NodeObject> unuseLightMapNode = new List<NodeObject>();
             //将树按照后续遍历展开
             var postTreeList = rootNode.PostOrderTraversalConverList();
             var classificationObjects = new Dictionary<long, NodeObject>();
             //每个 i 是一个 树的节点
             for (int i = 0; i < postTreeList.Count; i++)
             {
                 var tempGameObjects = postTreeList[i].Objects;


                 //这里的gameobject都是一个根节点，其下包含LOD——Mesh0-n——SubMesh
                 var meshRenderers = GetMeshRendererWithLOD(tempGameObjects);
                 foreach (var meshRenderer in meshRenderers)
                 {
                     var mats = meshRenderer.sharedMaterials;
                     var mesh = meshRenderer.gameObject.GetComponent<MeshFilter>().sharedMesh;
                     var light_mapindex = meshRenderer.lightmapIndex;
                     LightMode tempLightMode =
                         (light_mapindex >= 0 && light_mapindex < LightmapSettings.lightmaps.Length)
                             ? LightMode.LightMap
                             : LightMode.LightProbe;
                     //按照subMesh区分
                     for (int k = 0; k < mats.Length; k++)
                     {
                         var mat = mats[k];
                         int meshHash = mesh.GetHashCode();
                         int matHash = mat.GetHashCode();
                         long inde = long.Parse($"{meshHash}{matHash}");
                         if (classificationObjects.TryGetValue(inde, out var nodeObject))
                         {
                             MinGameObject temMinGameObj =
                                 new MinGameObject(meshRenderer, k, tempLightMode == LightMode.LightMap);
                             if (tempLightMode != nodeObject.m_lightMode)
                             {
                                 EditorUtility.DisplayDialog("警告",
                                     $"有OBJ的光照模型与所属类不同，请检查后重新设置,OBJ使用的是{meshRenderer.gameObject.name}", "确定");
                             }

                             nodeObject.AddMinGameObj(temMinGameObj);
                         }
                         else
                         {
                             NodeObject temNodeobj = new NodeObject(k, mesh, mat, inde, meshRenderer as Renderer);
                             MinGameObject temMinGameObj =
                                 new MinGameObject(meshRenderer, k, tempLightMode == LightMode.LightMap);
                             temNodeobj.AddMinGameObj(temMinGameObj);
                             classificationObjects.Add(inde, temNodeobj);
                         }
                     }
                 }

                 //将按照标识符（mesh——mat）分类的Node再按照是否使用lightmap分类，保证GIData和Matrix的位置一致.
                 foreach (var VARIABLE in classificationObjects)
                 {
                     if (VARIABLE.Key == 624068623448)
                     {
                         Debug.Log("");
                     }

                     if (VARIABLE.Value.m_lightMode == LightMode.LightMap)
                     {
                         useLightMapNode.Add(VARIABLE.Value);
                     }
                     else
                     {
                         unuseLightMapNode.Add(VARIABLE.Value);
                     }
                 }

                 classificationObjects.Clear();
             }

             foreach (var nodeObject in useLightMapNode)
             {
                 //一个nodeObject 是一种类型的mesh+mat，里面有很多的小的可渲染单位
                 if (nodeObject.m_lightMode == LightMode.LightMap)
                 {
                     result.AddNodeObject(nodeObject,true);
                 }
             }
             foreach (var nodeObject in unuseLightMapNode)
             {
                 if (nodeObject.m_lightMode == LightMode.LightProbe)
                 {
                     result.AddNodeObject(nodeObject,false);
                 }
             }
             useLightMapNode.Clear();
             unuseLightMapNode.Clear();
             //--------------------------------- 树分布 ---------------------------------------
             //                                   [0]
             //             [1]           [2]              [3]               [4]       
             //        [5][6][7][8]  [9][10][11][12] [13][14][15][16] [17][18][19][20] 
             //--------------------------   AllInstanceData 内数据分布 --------------------------
             //                                 使用LightMap                    
             //  [[5][6][7][8][1][9][10][11][12][2][13][14][15][16][3][17][18][19][20][4][0]]--
             //                                 不使用LightMap
             //  [[5][6][7][8][1][9][10][11][12][2][13][14][15][16][3][17][18][19][20][4][0]]--
             //---------------------------------- 单独节点内 ------------------------------------
             //---------- [[(Mesh+Mat_1)][(Mesh+Mat_2)][(Mesh+Mat_3)][(Mesh+Mat_4)]] ----------
             //--------------------------------------------------------------------------------
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

         public static List<MeshRenderer> GetMeshRendererWithLOD(List<GameObject> objects)
         {
             List<MeshRenderer> result = new List<MeshRenderer>();
             List<List<MeshRenderer>> tempMeshrenderer = new List<List<MeshRenderer>>();
             int maxLod = 1;
             //获取最大的Lod级别
             for (int i = 0; i < objects.Count; i++)
             {
                 var lodGroup = objects[i].GetComponent<LODGroup>();
                 if (lodGroup == null)
                 {
                     continue;
                 }
                 var lodCount = lodGroup.GetLODs().Length;
                 if (lodCount > maxLod)
                 {
                     maxLod = lodCount;
                 }
             }
             Debug.Log($"最大级别的Lod为{maxLod}");
             for (int i = 0; i < maxLod; i++)
             {
                 List<MeshRenderer> a = new List<MeshRenderer>();
                 tempMeshrenderer.Add(a);
             }
             for (int i = 0; i < objects.Count; i++)
             {
                 for (int j = 0; j < maxLod; j++)
                 {
                     tempMeshrenderer[j].AddRange(GetMeshRenderer.GetMeshRenderers(objects[i],0.01f,j,true));
                 }
             }

             for (int i = 0; i < maxLod; i++)
             {
                 result.AddRange(tempMeshrenderer[i]);
             }
             return result;
         }
    }
}