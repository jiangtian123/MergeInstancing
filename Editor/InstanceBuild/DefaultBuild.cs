using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using Unity.MergeInstancingSystem.CreateUtils;
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
        private string rootPatch;
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
            string subdirectory = $"{path}/{root.name}";
            //如果需要保存的路径不存在，就创建一个
            if (Directory.Exists(subdirectory) == false)
            {
                Directory.CreateDirectory(subdirectory);
            }
            rootPatch = $"{subdirectory}/{root.name}";
            //存按照某种遍历方式展成List的四叉树的容器
            TreeNodeContainer container = new TreeNodeContainer();
            int Root = ConvertNode(container,rootNode);
            List<InstanceGameObject> instanceGameObjects = new List<InstanceGameObject>();
            //按照树展成List的顺序将树节点中保存的GameObject放到数组中
            var instanceObjs = GetInstanceGameObj(container);
            InstancePrefab[] prefabs = GetInstancePrefabs(instanceObjs);
            InstanceSubSector[] uniqueSubSector = MergeInstanceSubSectors(prefabs);
            InstanceData instanceData = GetInstanceData(instanceObjs);
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
            defaultController.m_useJob = instance.UseJob;
            defaultController.m_root = Root;
            defaultController.m_jobBeginLevel = instance.BeginJobLevel;
            defaultController.m_treeNodeContainer = container;
            defaultController.m_instanceSector = prefabs;
            defaultController.m_gameobject = instanceGameObjects.ToArray();
            defaultController.m_subSectors = uniqueSubSector;
            defaultController.m_instanceData = instanceData;
            defaultController.m_castShadow = instance.CastShadow;
            
            //FileStream 二进制读写流，这里创建一个新的文件
            string filename = $"{subdirectory}/{root.name}_InstanceData.asset";
            AssetDatabase.CreateAsset(instanceData, filename);
            for (int i = 0; i < prefabs.Length; i++)
            {
                string tempName = $"{subdirectory}/{root.name}_Prefab{i}.asset";
                AssetDatabase.CreateAsset(prefabs[i], tempName);
                EditorUtility.SetDirty(prefabs[i]);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            var matrixsTexture =  SaveTexture(instanceData.m_matrixs,"matrixs",subdirectory);
            var lightMapOffestTexture = SaveTexture(instanceData.m_lightMapOffest,"lightMapOffest",subdirectory);
            instanceData.m_byteLightOffestTexture = lightMapOffestTexture;
            m_manager.AddGeneratedResource(lightMapOffestTexture);
            instanceData.m_byteMatrixTexture = matrixsTexture;
            m_manager.AddGeneratedResource(matrixsTexture);
            EditorUtility.SetDirty(defaultController);
            EditorUtility.SetDirty(instanceData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }


        private TextAsset SaveTexture(Texture2D texture2D,string name,string pach)
        {
            if (texture2D == null)
            {
                return null;
            }

            string filePath = $"{pach}/{name}.bytes";
            var byteData = texture2D.GetRawTextureData();
            File.WriteAllBytes(filePath, byteData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<TextAsset>(filePath);;
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
            List<DTransform> m_gameObjectData = new List<DTransform>();
            List<InstanceLightData> m_lightDataIndex = new List<InstanceLightData>();
            List<List<Matrix4x4>> m_instanceMatrixs = new List<List<Matrix4x4>>();
            for (int i = 0; i < instanceTargets.Count; i++)
            {
                var tempObj = instanceTargets[i];
                var gamobj = instanceTargets[i].obj.transform;
                m_gameObjectData.Add(new DTransform(gamobj.position, gamobj.rotation, gamobj.lossyScale));
                tempObj.insObj.m_dataIndex = m_instanceMatrixs.Count;
                tempObj.insObj.m_lightDataIndex = m_lightDataIndex.Count;
                var lightDatas = getInstanceLightDatas(instanceTargets[i].obj);
                var matrixDatas = getInstanceMatrixs(instanceTargets[i].obj);
                m_lightDataIndex.AddRange(lightDatas);
                m_instanceMatrixs.AddRange(matrixDatas);
            }
            TextureFormat format = TextureFormat.RGBAFloat;
            int lightTextureH = m_lightDataIndex.Count == 0 ? 1 : m_lightDataIndex.Count;
            Texture2D lightMapOffestTexture = 
                new Texture2D(1,lightTextureH , format, false);
            List<int> lightMapIndex = new List<int>();
            for (int i = 0; i < m_lightDataIndex.Count; i++)
            {
                var offest = m_lightDataIndex[i].m_lightOffest;
                var index = m_lightDataIndex[i].m_lightIndex;
                Color color = new Color(offest.x,offest.y,offest.z,offest.w);
                lightMapOffestTexture.SetPixel(0,i,color);
                lightMapIndex.Add(index);
            }
            lightMapOffestTexture.Apply();
            result.OffestTextureW = 1;
            result.OffestTextureH = lightTextureH;
            result.m_lightMapOffest = lightMapOffestTexture;
            result.m_lightMapIndex = lightMapIndex;
            Texture2D matrixsTexture = new Texture2D(8, m_instanceMatrixs.Count, format, false);
            for (int i = 0; i < m_instanceMatrixs.Count; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    var matrix = m_instanceMatrixs[i][j];
                    for (int k = 0; k < 4; k++)
                    {
                        var data = matrix.GetRow(k);
                        Color color = new Color(data.x,data.y,data.z,data.w);
                        matrixsTexture.SetPixel(j * 4 + k, i, color);
                    }
                }
            }
            matrixsTexture.Apply();
            result.m_matrixs = matrixsTexture;
            result.matrixTextureW = 8;
            result.matrixTextureH = matrixsTexture.height;
            
            result.m_gameobjTransform = m_gameObjectData;
            
            return result;
        }

        private List<InstanceLightData> getInstanceLightDatas(GameObject gameObject)
        {
            List<InstanceLightData> result = new List<InstanceLightData>();
            //放每层Lod下的Meshrenderer
            List<List<MeshRenderer>> tempMeshrenderer = new List<List<MeshRenderer>>();
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
                foreach (var meshRenderer in meshRenderers)
                {
                    InstanceLightData lightData = new InstanceLightData();
                    var light_mapindex = meshRenderer.lightmapIndex;
                    if (light_mapindex >= 0 && light_mapindex < LightmapSettings.lightmaps.Length)
                    {
                        lightData.m_lightIndex = light_mapindex;
                        lightData.m_lightOffest = meshRenderer.lightmapScaleOffset;
                        result.Add(lightData);
                    }
                }
            }
            return result;
        }

        private List<List<Matrix4x4>> getInstanceMatrixs(GameObject gameObject)
        {
            List<List<Matrix4x4>> result = new List<List<Matrix4x4>>();
            List<List<MeshRenderer>> tempMeshrenderer = new List<List<MeshRenderer>>();
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
            foreach (var meshRenderers in tempMeshrenderer)
            {
                foreach (var meshRenderer in meshRenderers)
                {
                    var localToWorldMatrix = meshRenderer.localToWorldMatrix;
                    var localToWorldMatrixInv = Matrix4x4.Inverse(localToWorldMatrix);
                    List<Matrix4x4> matrix4X4s = new List<Matrix4x4>{localToWorldMatrix,localToWorldMatrixInv};
                    result.Add(matrix4X4s);
                }
            }
            return result;
        }
        private InstancePrefab[] GetInstancePrefabs(List<TempGameobj> instanceTargets)
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
                //计算Lod
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
                result.GenerateBais();
                return result;
            }
            else
            {
                List<InstanceSector> sectors = new List<InstanceSector>();
                List<float> lodInfos = new List<float>();
                sectors.Add(GetInstanceSector(meshRenderers,gameObject));
                lodInfos.Add(0);
                Bounds bounds = meshRenderers[0].localBounds;
                for (int i = 1; i < meshRenderers.Count; i++)
                {
                    Bounds tempbox = meshRenderers[i].localBounds;
                    bounds.Encapsulate(tempbox);
                }
                result.m_box = bounds;
                result.m_LODInfos = lodInfos;
                result.m_lod = sectors.ToArray();
                result.GenerateBais();
                return result;
            }
        }

        private InstanceSector GetInstanceSector(LOD lod)
        {
            InstanceSector result = new InstanceSector();
            var renders = lod.renderers;
            List<InstanceSubSector> subSectors = new List<InstanceSubSector>();
            foreach (var mRender in renders)
            {
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

        private Dictionary<int, string> SubSectorMap = new Dictionary<int, string>();
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
            var light_mapindex = renderer.lightmapIndex;
            result.useLightMap = light_mapindex >= 0 && light_mapindex < LightmapSettings.lightmaps.Length;
            if (SubSectorMap.TryGetValue(result.GetHashCode(),out var guid))
            {
                return AssetDatabase.LoadAssetAtPath<InstanceSubSector>(AssetDatabase.GUIDToAssetPath(guid));
            }
            else
            {
                string tempName = $"{rootPatch}_SubSector{SubSectorMap.Count}.asset";
                AssetDatabase.CreateAsset(result, tempName);
                EditorUtility.SetDirty(result);
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                var sector = AssetDatabase.LoadAssetAtPath<InstanceSubSector>(tempName);
                SubSectorMap.Add(result.GetHashCode(),AssetDatabase.AssetPathToGUID(tempName));
                return sector;
            }
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
        private int ConvertNode(TreeNodeContainer container, SpaceNode rootNode)
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
            
            return 0;
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