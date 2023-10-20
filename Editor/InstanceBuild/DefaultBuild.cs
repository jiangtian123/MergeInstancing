using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Unity.MergeInstancingSystem;
using Unity.MergeInstancingSystem.SpaceManager;
using Unity.MergeInstancingSystem.Utils;
using Unity.MergeInstancingSystem.Controller;
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="rootNode">场景根节点</param>
        /// <param name="infos">场景中的Low Obj</param>
        /// <param name="instanceData"></param>
        /// <param name="root"></param>
        /// <param name="cullDistance"></param>
        /// <param name="lodDistance"></param>
        /// <param name="writeNoPrefab"></param>
        /// <param name="extractMaterial"></param>
        /// <param name="onProgress"></param>
        public void Build(SpaceNode rootNode, List<InstanceBuildInfo>
                infos, AllInstanceData instanceData, GameObject root, float cullDistance, float lodDistance,
            bool useMotionvector,bool writeNoPrefab, bool extractMaterial,
            Action<float> onProgress)
        {
            dynamic options = m_streamingOptions;
            string path = options.OutputDirectory+$"{SceneManager.GetActiveScene().name}";
            //存按照某种遍历方式展成List的四叉树的容器
            InstanceTreeNodeContainer container = new InstanceTreeNodeContainer();
            InstanceTreeNode convertedRootNode = ConvertNode(container, rootNode);
            BatchTreeNode(rootNode,infos,instanceData);
            if (onProgress != null)
                onProgress(0.0f);
            InstanceData data = GetInstanceData(instanceData);
            if (!CheckLightMode(instanceData,container,out var bojName))
            {
                EditorUtility.DisplayDialog("警告", $"有OBJ的光照模型与所属类不同，请检查后重新设置,OBJ使用的是{bojName}", "确定");
                Environment.Exit(0);
            }
            string filename = $"{path}/{root.name}.asset";
            //如果需要保存的路径不存在，就创建一个
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }
            //FileStream 二进制读写流，这里创建一个新的文件
            
            AssetDatabase.CreateAsset(data, filename);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            //里面存的是instance 渲染用到的真正的数据
          
            m_manager.AddGeneratedResource(data);
            var defaultController = root.AddComponent<DefaultInstanceController>();
            defaultController.ControllerID = m_controllerID;
            m_manager.AddGeneratedResource(defaultController);
            defaultController.InstanceData = data;
            defaultController.Container = container;
            defaultController.Root = convertedRootNode;
            defaultController.CullDistance = cullDistance;
            defaultController.LODDistance = lodDistance;
            defaultController.useMotionVector = useMotionvector;
        }
        private InstanceData GetInstanceData(AllInstanceData instanceData)
        {
            InstanceData tempData = new InstanceData();
            tempData.m_materials = instanceData.m_materials;
            tempData.m_meshs = instanceData.m_meshs;
            tempData.m_matCitationNumber = instanceData.m_matCitationNumber.ToArray();
            tempData.m_localToWorlds = instanceData.m_matrix4x4.ToArray();
            tempData.m_matAndMeshIdentifiers = instanceData.m_matAndMeshIdentifiers;
            tempData.m_identifierCounts = instanceData.m_identifierCounts;
            tempData.m_lightMapIndex = instanceData.m_lightMapIndex.ToArray();
            tempData.m_lightMapScaleOffset = instanceData.m_lightMapScaleOffset.ToArray();
            //tempData.m_LightProbes = instanceData.m_LightProbes.ToArray();
            tempData.m_identifierUseLightMapOrProbe = instanceData.m_identifierUseLightMapOrProbe;
            tempData.BatchMaterial();
            return tempData;
        }
        private void BatchTreeNode(SpaceNode node,List<InstanceBuildInfo> infos,AllInstanceData datas)
        {
            Queue<SpaceNode> spaceNodes = new Queue<SpaceNode>();
            Queue<InstanceTreeNode> instanceTreeNodes = new Queue<InstanceTreeNode>();
            spaceNodes.Enqueue(node);
            instanceTreeNodes.Enqueue(convertedTable[node]);
            while (spaceNodes.Count > 0)
            {
                var spaceNode = spaceNodes.Dequeue();
                var instanceTreeNode = instanceTreeNodes.Dequeue();
                if (spaceNode.HasChild())
                {
                    for (int i = 0; i < spaceNode.GetChildCount(); ++i)
                    {
                        spaceNodes.Enqueue(spaceNode.GetChild(i));
                        instanceTreeNodes.Enqueue(convertedTable[spaceNode.GetChild(i)]);
                    }
                }
                //处理每个节点
                var highNodeclassificationObject = spaceNode.classificationObjects;
                var highObj =  GetNodeData(highNodeclassificationObject,datas);
                instanceTreeNode.HighObjectIds = highObj;
            }

            foreach (var info in infos)
            {
                var instanceTreeNode = convertedTable[info.Target];
                var highNodeclassificationObject = info.classificationObjects;
                var lowObject =  GetNodeData(highNodeclassificationObject,datas);
                instanceTreeNode.LowObjectIds = lowObject;
            }
        }

        private bool CheckDuplicates(string path,string assetName)
        {
            string[] existingAssetPaths = AssetDatabase.FindAssets("t:Object", new[] { path });
            foreach (var existingAssetGuid in existingAssetPaths)
            {
                string existingAssetPath = AssetDatabase.GUIDToAssetPath(existingAssetGuid);
                string existingAssetName = System.IO.Path.GetFileName(existingAssetPath);

                if (existingAssetName == assetName)
                {

                    return true;

                }
            }

            return false;
        }
        /// <summary>
        /// 从分类好的OBJ中构建NodeData，按照Mesh和材质分类
        /// </summary>
        /// <param name="classificationObjects"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private List<NodeData> GetNodeData(Dictionary<string, List<NodeObject>> classificationObjects,AllInstanceData datas)
        {
            List<NodeData> result = new List<NodeData>();
            foreach (var nodePair in classificationObjects)
            {
                List<int> matrixx4x4Index = new List<int>();
                List<int> giIndex = new List<int>();
                //每种类型（材质加mesh）下的OBJList
                var nodelist = nodePair.Value;
                int meshIndex = datas.GetAssetsPositon(nodePair.Value[0].m_meshGUID,
                    AllInstanceData.AssetsEnum.mesh);
                int matIndex = datas.GetAssetsPositon(nodePair.Value[0].m_matGUID,
                    AllInstanceData.AssetsEnum.material);
                int subMesh = nodePair.Value[0].m_subMeshIndex;

                bool needLighMap = nodePair.Value[0].m_NeedLightMap;
                //收集所有的矩阵信息
                foreach (var nodeObject in nodelist)
                {
                    int index = datas.GetAssetsPositon(nodeObject.m_renderer.GetHashCode().ToString(),AllInstanceData.AssetsEnum.matrix4x4);
                    matrixx4x4Index.Add(index);
                    if (needLighMap)
                    {
                        int GIindex = datas.GetAssetsPositon(nodeObject.m_renderer.GetHashCode().ToString(),AllInstanceData.AssetsEnum.LightMapIndex);
                        if (GIindex == -1)
                        {
                            Debug.Log("has bug");
                        }
                        giIndex.Add(GIindex);
                    }
                    // else
                    // {
                    //     int GIindex = datas.GetAssetsPositon(nodeObject.m_renderer.GetHashCode().ToString(),AllInstanceData.AssetsEnum.SH);
                    //     giIndex.Add(GIindex);
                    // }
                }
                matrixx4x4Index.Sort();
                var matrixs = matrixx4x4Index.SplitArrayIntoConsecutiveSubArrays((a, b) =>
                {
                    if (a == b+1)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });
                var giList = giIndex.SplitArrayIntoConsecutiveSubArrays((a, b) =>
                {
                    if (a == b + 1)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });

                // ----------------------------------- 
                NodeData tempNodteData = new NodeData();
                tempNodteData.m_GIData = giList;
                tempNodteData.m_matrix4x4Data = matrixs;
                tempNodteData.m_material = matIndex;
                tempNodteData.m_meshIndex = meshIndex;
                tempNodteData.subMeshIndex = subMesh;
                tempNodteData.m_castShadow = nodePair.Value[0].m_castShadow;
                tempNodteData.m_queue = nodePair.Value[0].m_queue;
                tempNodteData.m_identifier = datas.GetIdentifier(meshIndex, matIndex);
                tempNodteData.m_NeedLightMap = needLighMap;
                result.Add(tempNodteData);
            }

           
            return result;
        }
        Dictionary<SpaceNode, InstanceTreeNode> convertedTable = new Dictionary<SpaceNode, InstanceTreeNode>();
        /// <summary>
        /// 这个地方按照 子节点父节点的方式展开树。
        /// </summary>
        /// <param name="container"></param>
        /// <param name="rootNode"></param>
        /// <returns></returns>
        private InstanceTreeNode ConvertNode(InstanceTreeNodeContainer container, SpaceNode rootNode)
        {
            InstanceTreeNode root = new InstanceTreeNode();
            root.SetContainer(container);
            
            Queue<InstanceTreeNode> instanceTreeNodes = new Queue<InstanceTreeNode>();
            Queue<SpaceNode> spaceNodes = new Queue<SpaceNode>();
            Queue<int> levels = new Queue<int>();
            
            instanceTreeNodes.Enqueue(root);
            spaceNodes.Enqueue(rootNode);
            levels.Enqueue(0);
            int COunt = 0;
            while (instanceTreeNodes.Count > 0)
            {
                COunt++;
                var instanceTreeNode = instanceTreeNodes.Dequeue();
                var spaceNode = spaceNodes.Dequeue();
                int level = levels.Dequeue();
    
                convertedTable[spaceNode] = instanceTreeNode;
                if (COunt == 57)
                {
                    Debug.Log("");
                }
                instanceTreeNode.Level = level;
                instanceTreeNode.Bounds = spaceNode.Bounds;
                instanceTreeNode.CullBounds = spaceNode.CalculateRealBound();
                if (spaceNode.HasChild())
                {
                    //存放一个和一个节点子孩子数量相同的TreeNode
                    List<InstanceTreeNode> childTreeNodes = new List<InstanceTreeNode>(spaceNode.GetChildCount());
                    for (int i = 0; i < spaceNode.GetChildCount(); ++i)
                    {
                        var treeNode = new InstanceTreeNode();
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

        private bool CheckLightMode(AllInstanceData instanceData,InstanceTreeNodeContainer instanceTreeNodeContainer,out string gameBojName)
        {
            for (int i = 0; i < instanceTreeNodeContainer.Count; i++)
            {
                var treeNode = instanceTreeNodeContainer.Get(i);
                foreach (var nodeData in treeNode.HighObjectIds)
                {
                    var indefi = nodeData.m_identifier;
                    if (nodeData.m_NeedLightMap == instanceData.GetIdentifierLightMode(indefi))
                    {
                        continue;
                    }
                    else
                    {
                        var mat = instanceData.GetMat(nodeData.m_material);
                        var mesh = instanceData.GetMesh(nodeData.m_meshIndex);
                        gameBojName = $"{mat.name}+{mesh.name}";
                        return false;
                    }
                }
                foreach (var nodeData in treeNode.LowObjectIds)
                {
                    var indefi = nodeData.m_identifier;
                    if (nodeData.m_NeedLightMap == instanceData.GetIdentifierLightMode(indefi))
                    {
                        continue;
                    }
                    else
                    {
                        var mat = instanceData.GetMat(nodeData.m_material);
                        var mesh = instanceData.GetMesh(nodeData.m_meshIndex);
                        gameBojName = $"{mat.name}+{mesh.name}";
                        return false;
                    }
                }
            }

            gameBojName = "";
            return true;
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