using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Unity.MergeInstancingSystem.CreateUtils;
using Unity.MergeInstancingSystem.CustomData;
using Unity.MergeInstancingSystem.New;
using Unity.MergeInstancingSystem.SpaceManager;
using Unity.MergeInstancingSystem.Utils;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace Unity.MergeInstancingSystem.InstanceBuild
{
    public class DefaultBuild : IInstanceBuild
    {
        [InitializeOnLoadMethod]
        static void RegisterType()
        {
            InstanceBuilderTypes.RegisterType(typeof(DefaultBuild), -1);
        }
        
        private IGeneratedResourceManager m_manager;
        private SerializableDynamicObject m_streamingOptions;
        private int m_controllerID;

        public DefaultBuild(IGeneratedResourceManager manager, int controllerID, SerializableDynamicObject streamingOptions)
        {
            m_manager = manager;
            m_streamingOptions = streamingOptions;
            m_controllerID = controllerID;
        }
        public void Build(SpaceNode rootNode, GameObject root,Instance instance, Action<float> onProgress)
        {
            dynamic options = m_streamingOptions;
            string path = options.OutputDirectory+$"{SceneManager.GetActiveScene().name}";
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }
            //存按照某种遍历方式展成List的四叉树的容器
            TreeNodeContainer container = new TreeNodeContainer();
            TreeNode Root = ConvertNode(container,rootNode);
            List<InstanceGameObject> instanceGameObjects = new List<InstanceGameObject>();

            var instanceObjs = GetInstanceGameObj(container);
            
            InstanceData instanceData = GetInstanceData(instanceObjs);
            InstancePrefab[] prefabs = GetInstancePrefab(instanceObjs);
            InstanceSubSector[] uniqueSubSector = MergeInstanceSubSectors(prefabs);
            for (int i = 0; i < instanceObjs.Count; i++)
            {
                instanceGameObjects.Add(instanceObjs[i].insObj);
            }
            CalculateSubsectorRef(instanceGameObjects,prefabs,uniqueSubSector);
            if (onProgress != null)
                onProgress(0.0f);
            //里面存的是instance 渲染用到的真正的数据
            for (int i = 0; i < prefabs.Length; i++)
            {
                m_manager.AddGeneratedResource(prefabs[i]);
            }

            for (int i = 0; i < uniqueSubSector.Length; i++)
            {
                m_manager.AddGeneratedResource(uniqueSubSector[i]);
            }
            m_manager.AddGeneratedResource(instanceData);
            TreeNodeController defaultController = root.AddComponent<TreeNodeController>();
            m_manager.AddGeneratedResource(defaultController);
            defaultController.m_cullDistance = instance.CullDistance;
            defaultController.m_useJob = instance.UseJob;
            defaultController.m_root = Root;
            defaultController.m_jobBeginLevel = instance.BeginJobLevel;
            defaultController.m_useMotionvecter = instance.UseMotionvector;
            defaultController.m_treeNodeContainer = container;
            defaultController.m_instanceSector = prefabs;
            defaultController.m_gameobject = instanceGameObjects.ToArray();
            defaultController.m_subSectors = uniqueSubSector;
            defaultController.m_instanceData = instanceData;
            string subdirectory = $"{path}/{root.name}";
            //如果需要保存的路径不存在，就创建一个
            if (Directory.Exists(subdirectory) == false)
            {
                Directory.CreateDirectory(subdirectory);
            }
            //FileStream 二进制读写流，这里创建一个新的文件
            string filename = $"{subdirectory}/{root.name}_InstanceData.asset";
            AssetDatabase.CreateAsset(instanceData, filename);
            for (int i = 0; i < prefabs.Length; i++)
            {
                string tempName = $"{subdirectory}/{root.name}_Prefab{i}.asset";
                AssetDatabase.CreateAsset(prefabs[i], tempName);
            }
            for (int i = 0; i < uniqueSubSector.Length; i++)
            {
                string tempName = $"{subdirectory}/{root.name}_SubSector{i}.asset";
                AssetDatabase.CreateAsset(uniqueSubSector[i], tempName);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        
        
        public class TempGameobj
        {
            public GameObject obj;
            public InstanceGameObject insObj;
        }

        private void CalculateSubsectorRef(List<InstanceGameObject> instanceGameObje,InstancePrefab[] prefabs,InstanceSubSector[] subSectors)
        {
            Dictionary<InstanceSubSector, int> counter = new Dictionary<InstanceSubSector, int>();
            foreach (var instanceGameObject in instanceGameObje)
            {
                var prefab = prefabs[instanceGameObject.m_prefabIndex];
                foreach (var instanceSector in prefab.m_lod)
                {
                    foreach (var instanceSubSector in instanceSector.m_subSectors)
                    {
                        if (counter.TryGetValue(instanceSubSector,out var number))
                        {
                            counter[instanceSubSector] = number + 1;
                        }
                        else
                        {
                            counter.Add(instanceSubSector,1);
                        }
                    }
                }
            }

            foreach (var instanceSubSector in subSectors)
            {
                instanceSubSector.sectionCount = counter[instanceSubSector];
            }
        }
        private List<TempGameobj> GetInstanceGameObj(TreeNodeContainer treeList)
        {
            Dictionary<int, InstanceSubSector> subSectors = new Dictionary<int, InstanceSubSector>();
            List<TempGameobj> instanceTargets = new List<TempGameobj>();
            
            for (int i = 0; i < treeList.Count; i++)
            {
                var treeNode = treeList.Get(i);
                SpaceNode spaceNode = convertedTable[treeNode];
                treeNode.m_Gameobj.head = instanceTargets.Count;
                for (int j = 0; j < spaceNode.Objects.Count; j++)
                {
                    //这个gameobj是预制体的父物体，其下有lod
                    GameObject gameObject = spaceNode.Objects[j];
                    InstanceGameObject instanceGameObject = new InstanceGameObject();
                    TempGameobj a = new TempGameobj();
                    a.obj = gameObject;
                    a.insObj = instanceGameObject;
                    instanceTargets.Add(a);
                }
                treeNode.m_Gameobj.number = spaceNode.Objects.Count;
            }
            return instanceTargets;
           
        }

        private InstanceData GetInstanceData( List<TempGameobj> instanceTargets)
        {
            InstanceData result = new InstanceData();
            List<SerializableData> m_gameObjectData = new List<SerializableData>();
            for (int i = 0; i < instanceTargets.Count; i++)
            {
                var tempObj = instanceTargets[i];
                tempObj.insObj.m_dataIndex = i;
                var data = GetSerializableData(tempObj.obj);
                m_gameObjectData.Add(data);
            }

            result.m_gameObjectData = m_gameObjectData;
            return result;
        }

        private SerializableData GetSerializableData(GameObject gameObject)
        {
            SerializableData result = new SerializableData();
            //放每层Lod下的Meshrenderer
            List<List<MeshRenderer>> tempMeshrenderer = new List<List<MeshRenderer>>();

            List<LodSerializableData> tempLodData = new List<LodSerializableData>();
            //按照Lod级别拿的MeshRender
            int maxLod = 1;
            //获取最大的Lod级别
            var lodGroup = gameObject.GetComponent<LODGroup>();
            if (lodGroup != null)
            {
                maxLod = lodGroup.GetLODs().Length;
            }
            for (int i = 0; i < maxLod; i++)
            {
                List<MeshRenderer> a = new List<MeshRenderer>();
                tempMeshrenderer.Add(a);
            }
            for (int j = 0; j < maxLod; j++)
            {
                tempMeshrenderer[j].AddRange(GetMeshRenderer.GetMeshRenderers(gameObject,0.01f,j,true));
            }
            //每个meshRenderers 是一种lod下的所有meshrenderer
            foreach (var meshRenderers in tempMeshrenderer)
            {
                List<DTransform> localToworld = new List<DTransform>();
                List<float> lightmapIndex = new List<float>();
                List<Vector4> lisghtmapOffest = new List<Vector4>();
                foreach (var meshRenderer in meshRenderers)
                {
                    localToworld.Add(new DTransform(meshRenderer.transform.position,meshRenderer.transform.rotation,meshRenderer.transform.lossyScale));
                    var light_mapindex = meshRenderer.lightmapIndex;
                    if (light_mapindex >= 0 && light_mapindex < LightmapSettings.lightmaps.Length)
                    {
                        lightmapIndex.Add((float)light_mapindex);
                        lisghtmapOffest.Add(meshRenderer.lightmapScaleOffset);
                    }
                }

                LodSerializableData temp = new LodSerializableData();
                temp.transforms = localToworld.ToArray();
                temp.lightmapIndex = lightmapIndex.ToArray();
                temp.lightmapOffest = lisghtmapOffest.ToArray();
                tempLodData.Add(temp);
            }
            result.m_LodData = tempLodData.ToArray();
            return result;
        }


        private InstancePrefab[] GetInstancePrefab(List<TempGameobj> instanceTargets)
        {
            List<InstancePrefab> result = new List<InstancePrefab>();
            Dictionary<string, int> tempInstancePrefabs = new Dictionary<string, int>();
            //收集所有的prefab
            foreach (var tempGameobj in instanceTargets)
            {
                var gameobj = tempGameobj.obj;
                string prefabGUID = GetObjectGUID(gameobj);
                if (prefabGUID == "")
                {
                    continue;
                }
                if (!tempInstancePrefabs.ContainsKey(prefabGUID))
                {
                    result.Add(GetInstancePrefab(gameobj));
                    tempInstancePrefabs.Add(prefabGUID, result.Count - 1);
                }
            }

            foreach (var tempGameobj in instanceTargets)
            {
                var gameobj = tempGameobj.obj;
                string prefabGUID = GetObjectGUID(gameobj);
                if (prefabGUID == "")
                {
                    continue;
                }
                tempGameobj.insObj.m_prefabIndex = tempInstancePrefabs[prefabGUID];
            }
            return result.ToArray();
        }
            

        public InstancePrefab GetInstancePrefab(GameObject gameObject)
        {
            InstancePrefab result = new InstancePrefab();
            List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
            var lodGroup = gameObject.GetComponent<LODGroup>();
            meshRenderers.AddRange(gameObject.GetComponentsInChildren<MeshRenderer>());
            RemoveDisabled(meshRenderers);
            if (lodGroup != null)
            {
                LOD[] lods = lodGroup.GetLODs();
                Renderer[] lodRenderers = lods[0].renderers;
                Bounds bounds = lodRenderers[0].localBounds;
                for (int i = 1; i < lodRenderers.Length; i++)
                {
                    Bounds tempbox = lodRenderers[i].localBounds;
                    bounds.Encapsulate(tempbox);
                }

                result.m_box = bounds;
                List<InstanceSector> sectors = new List<InstanceSector>();
                List<float> lodInfos = new List<float>();
                for (int i = 0; i < lods.Length; i++)
                {
                    LOD lod = lods[i];
                    sectors.Add(GetInstanceSector(lod));
                    lodInfos.Add(lod.screenRelativeTransitionHeight);
                }
                result.m_lod = sectors.ToArray();
                result.m_LODInfos = lodInfos;
                return result;
            }
            else
            {
                List<InstanceSector> sectors = new List<InstanceSector>();
                List<float> lodInfos = new List<float>();
                sectors.Add(GetInstanceSector(meshRenderers,gameObject));
                lodInfos.Add(1);
                Bounds bounds = meshRenderers[0].localBounds;
                for (int i = 1; i < meshRenderers.Count; i++)
                {
                    Bounds tempbox = meshRenderers[i].localBounds;
                    bounds.Encapsulate(tempbox);
                }
                result.m_box = bounds;
                result.m_LODInfos = lodInfos;
                result.m_lod = sectors.ToArray();
                return result;
            }
        }

        private InstanceSector GetInstanceSector(LOD lod)
        {
            InstanceSector result = new InstanceSector();
            var renders = lod.renderers;
            List<Matrix4x4> tempMatrix = new List<Matrix4x4>();
            List<InstanceSubSector> subSectors = new List<InstanceSubSector>();
            foreach (var mRender in renders)
            {
                tempMatrix.Add(Matrix4x4.TRS(mRender.transform.localPosition,mRender.transform.localRotation,mRender.transform.localScale));
                subSectors.Add(GetInstanceSubSector(mRender));
            }

            result.m_subSectors = subSectors.ToArray();
            return result;
        }
        private InstanceSector GetInstanceSector(List<MeshRenderer> meshRenderers,GameObject root)
        {
            InstanceSector result = new InstanceSector();
            List<InstanceSubSector> subSectors = new List<InstanceSubSector>();
            foreach (var mRender in meshRenderers)
            {
                subSectors.Add(GetInstanceSubSector(mRender));
            }
            result.m_subSectors = subSectors.ToArray();
            return result;
        }
        
        private InstanceSubSector GetInstanceSubSector(Renderer renderer)
        {
            InstanceSubSector result = new InstanceSubSector();
            var mesh = renderer.gameObject.GetComponent<MeshFilter>().sharedMesh;
            var mats = renderer.sharedMaterials;
            List<int> subMeshIndex = new List<int>();
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                subMeshIndex.Add(i);
            }
            result.sectionCount = 0;
            result.m_subMeshIndex = subMeshIndex.ToArray();
            result.m_mesh = mesh;
            result.materials = mats;
            result.m_Renderqueue = (RenderQueue)mats[0].renderQueue;
            result.m_castShadow = renderer.shadowCastingMode == ShadowCastingMode.On;
            return result;
        }
        private static string GetObjectGUID(GameObject gameObject)
        {
            PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(gameObject);
            if (prefabAssetType == PrefabAssetType.Regular || prefabAssetType == PrefabAssetType.Variant)
            {
                UnityEngine.Object prefabObject = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                string prefabGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefabObject));
                return prefabGUID;
            }
            else
            {
                return "";
            }
        }

        private InstanceSubSector[] MergeInstanceSubSectors(InstancePrefab[] prefabs)
        {
            HashSet<InstanceSubSector> unique = new HashSet<InstanceSubSector>();
            for (int i = 0; i < prefabs.Length; i++)
            {
                var lods = prefabs[i].m_lod;
                foreach (var instanceSector in lods)
                {
                    foreach (var instanceSubSector in instanceSector.m_subSectors)
                    {
                        unique.Add(instanceSubSector);
                    }
                }
            }
            List<InstanceSubSector> array = new List<InstanceSubSector>(unique);
            for (int i = 0; i < prefabs.Length; i++)
            {
                var lods = prefabs[i].m_lod;
                foreach (var instanceSector in lods)
                {
                    List<int> subSectorIndex = new List<int>();
                    for (int j = 0; j < instanceSector.m_subSectors.Length; j++)
                    {
                        var instanceSubSector = instanceSector.m_subSectors[j];
                        int index = array.IndexOf(instanceSubSector);
                        subSectorIndex.Add(index);
                    }
                    instanceSector.m_meshs = subSectorIndex.ToArray();
                }
            }
            return array.ToArray();
        }

        Dictionary<TreeNode,SpaceNode> convertedTable = new Dictionary<TreeNode,SpaceNode>();
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
        
        /// <summary>
        /// 这个地方按照 子节点父节点的方式展开树。
        /// </summary>
        /// <param name="container"></param>
        /// <param name="rootNode"></param>
        /// <returns></returns>
        private TreeNode ConvertNode(TreeNodeContainer container, SpaceNode rootNode)
        {
            TreeNode root = new TreeNode();
            root.SetContainer(container);
            container.Add(root);
            Queue<TreeNode> instanceTreeNodes = new Queue<TreeNode>();
            Queue<SpaceNode> spaceNodes = new Queue<SpaceNode>();
            Queue<int> levels = new Queue<int>();
            
            instanceTreeNodes.Enqueue(root);
            spaceNodes.Enqueue(rootNode);
            levels.Enqueue(0);
            while (instanceTreeNodes.Count > 0)
            {
                var instanceTreeNode = instanceTreeNodes.Dequeue();
                var spaceNode = spaceNodes.Dequeue();
                int level = levels.Dequeue();

                convertedTable[instanceTreeNode] = spaceNode;
                instanceTreeNode.m_level = level;
                instanceTreeNode.m_Box = spaceNode.Bounds;
                instanceTreeNode.hasChild = spaceNode.HasChild();
                if (spaceNode.HasChild())
                {
                    //存放一个和一个节点子孩子数量相同的TreeNode
                    List<TreeNode> childTreeNodes = new List<TreeNode>(spaceNode.GetChildCount());
                    for (int i = 0; i < spaceNode.GetChildCount(); ++i)
                    {
                        var treeNode = new TreeNode();
                        treeNode.SetContainer(container);
                        childTreeNodes.Add(treeNode);

                        instanceTreeNodes.Enqueue(treeNode);
                        spaceNodes.Enqueue(spaceNode.GetChild(i));
                        levels.Enqueue(level + 1);
                    }
                    instanceTreeNode.SetChildTreeNode(childTreeNodes);
                }
            }
            
            return root;
        }
        public static void OnGUI(SerializableDynamicObject buildingOptions)
        {
            dynamic options = buildingOptions;
            #region Setup default values

            if (options.OutputDirectory == null)
            {
                string path = Application.dataPath;
                path = "Assets" + path.Substring(Application.dataPath.Length);
                path = path.Replace('\\', '/');
                if (path.EndsWith("/") == false)
                    path += "/";
                options.OutputDirectory = path;
            }
            #endregion
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("OutputDirectory");
            if (GUILayout.Button(options.OutputDirectory))
            {
                string selectPath = EditorUtility.OpenFolderPanel("Select output folder", "Assets", "");

                if (selectPath.StartsWith(Application.dataPath))
                {
                    selectPath = "Assets" + selectPath.Substring(Application.dataPath.Length);
                    selectPath = selectPath.Replace('\\', '/');
                    if (selectPath.EndsWith("/") == false)
                        selectPath += "/";
                    options.OutputDirectory = selectPath;
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", $"Select directory under {Application.dataPath}", "OK");
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}