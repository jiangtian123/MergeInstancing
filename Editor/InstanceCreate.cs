using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.MergeInstancingSystem.Utils;
using Unity.MergeInstancingSystem.CreateUtils;
using Unity.MergeInstancingSystem.InstanceBuild;
using Unity.MergeInstancingSystem.MeshUtils;
using Unity.Collections;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Unity.MergeInstancingSystem.SpaceManager;
namespace Unity.MergeInstancingSystem
{
    /// <summary>
    /// 处理每个Instance的工具类
    /// </summary>
    public class InstanceCreate
    {
        private struct TravelQueueItem
        {
            /// <summary>
            /// 节点
            /// </summary>
            public SpaceNode Node;
            /// <summary>
            /// 当前节点的父节点在第几层中的第几个，从0开始
            /// </summary>
            public int Parent;
            /// <summary>
            /// 名字几个_就是第几层，数字是所在层的位置
            /// </summary>
            public string Name;
            /// <summary>
            /// 当前节点所在树的第几层
            /// </summary>
            public int Level;
            /// <summary>
            /// 当前节点保存的物体，包含子节点的
            /// </summary>
            public List<GameObject> TargetGameObjects;
            /// <summary>
            /// 距离
            /// </summary>
            public List<int> Distances;
        }
        /// <summary>
        /// 将下层的节点包含的物体逐层的拷贝到父级
        /// </summary>
        /// <param name="list">处理过的对象，列表最后一个是正在处理的</param>
        /// <param name="curIndex">当前处理的在第几层的第几个为i+j+1</param>
        /// <param name="objects">当前节点有多少个物体</param>
        /// <param name="distance"></param>
        private static void CopyObjectsToParent(List<TravelQueueItem> list, int curIndex, List<GameObject> objects, int distance)
        {
            if (curIndex < 0)
                return;

            int parentIndex = list[curIndex].Parent;
            
            if (parentIndex < 0)
                return;
            //parent 是父节点在链表中的位置
            var parent = list[parentIndex];

            parent.TargetGameObjects.AddRange(objects);
            parent.Distances.AddRange(Enumerable.Repeat<int>(distance, objects.Count));
            
            CopyObjectsToParent(list, parentIndex, objects, distance + 1);

        }
        /// <summary>
        /// 按照层级去LodGroup中取数据
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="root"></param>
        /// <param name="minObjectSize"></param>
        /// <returns></returns>
        private static List<InstanceBuildInfo> CreateBuildInfo(Instance instance, SpaceNode root, float minObjectSize)
        {
            List<InstanceBuildInfo> result = new List<InstanceBuildInfo>();
            //还剩几个没处理
            Queue<TravelQueueItem> travelQueue = new Queue<TravelQueueItem>();
            //这个里面每个节点都包含自己及子节点的所有renderer
            List<TravelQueueItem> candidateItems = new List<TravelQueueItem>();
            //处理完的
            List<InstanceBuildInfo> buildInfoCandidates = new List<InstanceBuildInfo>();
            
            int maxLevel = 0;
            
            travelQueue.Enqueue(new TravelQueueItem()
            {
                Node = root,
                Parent = -1,
                Level = 0,
                Name = "",
                TargetGameObjects = new List<GameObject>(),
                Distances = new List<int>(),
            });
            //按层遍历树
            while (travelQueue.Count > 0)
            {
                //当前节点在树中的index，同层级按照 左下，左上，右下，右上
                int currentNodeIndex = candidateItems.Count;
                TravelQueueItem item = travelQueue.Dequeue();
                for (int i = 0; i < item.Node.GetChildCount(); ++i)
                {
                    travelQueue.Enqueue(new TravelQueueItem()
                    {
                        Node = item.Node.GetChild(i),
                        Parent = currentNodeIndex,
                        Level = item.Level + 1,
                        Name = item.Name + "_" + (i+1),
                        TargetGameObjects = new List<GameObject>(),
                        Distances = new List<int>(),
                    });
                }
                maxLevel = Math.Max(maxLevel, item.Level);
                candidateItems.Add(item);
                buildInfoCandidates.Add(new InstanceBuildInfo()
                {
                    Name = item.Name,
                    ParentIndex = item.Parent,
                    Target = item.Node
                });
                item.TargetGameObjects.AddRange(item.Node.Objects);
                item.Distances.AddRange(Enumerable.Repeat<int>(0, item.Node.Objects.Count));
                CopyObjectsToParent(candidateItems, currentNodeIndex, item.Node.Objects, 1);
            }
            //遍历每个处理好的节点，处理其InstanceBuildInfo
            for (int i = 0; i < candidateItems.Count; ++i)
            {
                var info = buildInfoCandidates[i];
                var item = candidateItems[i];
                //maxLevel是当前四叉树的总层级，item.Level是当前节点所在的层级
                var meshRenderers = new List<MeshRenderer>();
                var distances = new List<int>();
                //拿一个Obj的MeshRenderer
                for (int ti = 0; ti < item.TargetGameObjects.Count; ++ti)
                {
                    var curDistance = item.Distances[ti];
                    var curRenderers = GetMeshRenderer.GetMeshRenderers(item.TargetGameObjects[ti], minObjectSize,curDistance+1);
                    meshRenderers.AddRange(curRenderers);
                    distances.AddRange(Enumerable.Repeat<int>(curDistance, curRenderers.Count));
                }
                for (int mi = 0; mi < meshRenderers.Count; ++mi)
                {
                    info.AddWorkingObject(meshRenderers[mi]);
                    info.Distances.Add(distances[mi]);
                }
                
            }
            for (int i = 0; i < buildInfoCandidates.Count; ++i)
            {
                if (buildInfoCandidates[i].classificationObjects.Count > 0)
                {
                    result.Add(buildInfoCandidates[i]);
                }
            }
            return result;
        }
        
        public static IEnumerator Create(Instance ins)
        {
            try
            {
                Stopwatch sw = new Stopwatch();
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                sw.Reset();
                sw.Start();
                //--------------- 获取ins下所有renderer，并且构建一个以ins为坐标原点的包含所有renderer的包围盒 -------------------------
                Bounds bounds = ins.GetBounds();
                //--------------- 获取所有的渲染OBj(ins下的所有单独实例) ----------------------------------------------------------
                List<GameObject> instanceTargets = ObjectUtils.InstanceTargets(ins.gameObject);
                //--------------- 处理获得的包围盒 ------------------------------------------------------------------------------
                //按照在UI面板设置的类型初始化分割对象
                ISpaceSplitter spliter = SpaceSplitterTypes.CreateInstance(ins);
                //如果没有空间划分的对象就报错
                if (spliter == null)
                {
                    EditorUtility.DisplayDialog("SpaceSplitter not found",
                        "There is no SpaceSplitter. Please set the SpaceSplitter.",
                        "OK");
                    yield break;
                }
                //开始划分
                List<SpaceNode> rootNodeList = spliter.CreateSpaceTree(bounds, ins.ChunkSize, ins.transform, instanceTargets, progress =>
                {
                    EditorUtility.DisplayProgressBar("Bake Instance", "Splitting space", progress * 0.25f);
                });
                //如果该instance下没有实例
                if (instanceTargets.Count == 0)
                {
                    EditorUtility.DisplayDialog("Empty Instance sources.",
                        "There are no objects to be included in the Instance.",
                        "Ok");
                    yield break;
                }
                //一个空间不能划分超过256块
                if (rootNodeList.Count >= 256)
                {
                    EditorUtility.DisplayDialog("Too many SubInstanceTrees.",
                        "There are too many SubInstanceTrees. SubInstanceTrees is supported less than 256.",
                        "Ok");
                    yield break;
                }
                for (int ri = 0; ri < rootNodeList.Count; ++ri)
                {
                    var rootNode = rootNodeList[ri];
                    GameObject targetGameObject = ins.gameObject;
                    if (rootNodeList.Count > 1)
                    {
                        GameObject newTargetGameObject = new GameObject($"{targetGameObject.name}_SubTree{ri}");
                        newTargetGameObject.transform.SetParent(targetGameObject.transform, false);
                        ins.AddGeneratedResource(newTargetGameObject);

                        targetGameObject = newTargetGameObject;
                    }
                    IInstanceBuild builder =
                        (IInstanceBuild)Activator.CreateInstance(ins.BuildType,
                            new object[] { ins, ri, ins.BuildOptions });
                    builder.Build(rootNode, targetGameObject, ins,
                        progress =>
                        {
                            EditorUtility.DisplayProgressBar("Bake Instance", "Storing results.",
                                0.75f + progress * 0.25f);
                        });
                    
                }

                foreach (var gameObject in instanceTargets)
                {
                    var meshRenders = gameObject.GetComponentsInChildren<MeshRenderer>();
                    foreach (var meshRender in meshRenders)
                    {
                        ins.m_DisAbleComponent.Add(meshRender);
                        meshRender.enabled = false;
                    }
                    var meshs = gameObject.GetComponentsInChildren<MeshFilter>();
                    foreach (var mesh in meshs)
                    {
                        ins.m_DestoryMesh.Add(mesh.sharedMesh);
                        ins.m_DestoryOBJ.Add(mesh.gameObject);
                        GameObject.DestroyImmediate(mesh,true);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                
            }
        }

        public static IEnumerator Destroy(Instance instance)
        {
            if (instance.GeneratedObjects.Count == 0)
                yield break;
            try
            {
                EditorUtility.DisplayProgressBar("Destroy instance", "Destrying instance files", 0.0f);
                var convertedPrefabObjects = instance.ConvertedPrefabObjects;
                for (int i = 0; i < convertedPrefabObjects.Count; ++i)
                {
                    PrefabUtility.UnpackPrefabInstance(convertedPrefabObjects[i], PrefabUnpackMode.OutermostRoot,
                        InteractionMode.AutomatedAction);
                }
                var controllers = instance.GetInstanceControllerBase();
                var generatedObjects = instance.GeneratedObjects;
                for (int i = 0; i < generatedObjects.Count; ++i)
                {
                    if (generatedObjects[i] == null)
                        continue;
                    var path = AssetDatabase.GetAssetPath(generatedObjects[i]);
                    if (string.IsNullOrEmpty(path) == false)
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                    else
                    {
                        Object.DestroyImmediate(generatedObjects[i]);
                    }
                    EditorUtility.DisplayProgressBar("Destroy Instance", "Destrying Instance files", (float)i / (float)generatedObjects.Count);
                }
                generatedObjects.Clear();
                for (int i = 0; i < controllers.Count; ++i)
                {
                    if (controllers[i] == null)
                        continue;

                    Object.DestroyImmediate(controllers[i]);
                }

                foreach (var meshRenderer in instance.m_DisAbleComponent)
                {
                    meshRenderer.enabled = true;
                }
                instance.m_DisAbleComponent.Clear();
                for (int i = 0; i < instance.m_DestoryOBJ.Count; i++)
                {
                    var gameobject = instance.m_DestoryOBJ[i];
                    var meshfilter = gameobject.GetComponent<MeshFilter>();
                    if (meshfilter == null)
                    {
                        meshfilter = gameobject.AddComponent<MeshFilter>();
                    }
                    var mesh = instance.m_DestoryMesh[i];
                    meshfilter.sharedMesh = mesh;
                }
                instance.m_DestoryOBJ.Clear();
                instance.m_DestoryMesh.Clear();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            EditorUtility.SetDirty(instance.gameObject);
            EditorUtility.SetDirty(instance);
        }
    }
}