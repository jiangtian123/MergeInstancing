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
        public void Build(SpaceNode rootNode,List<InstanceBuildInfo> 
            info,AllInstanceData instanceData, GameObject root,float cullDistance, float lodDistance, bool writeNoPrefab,bool useMotionvector,bool usePreciseCulling ,bool extractMaterial, Action<float> onProgress)
        {
            dynamic options = m_streamingOptions;
            string path = options.OutputDirectory+$"{SceneManager.GetActiveScene().name}";
            //存按照某种遍历方式展成List的四叉树的容器
            InstanceTreeNodeContainer container = new InstanceTreeNodeContainer();
            InstanceTreeNode convertedRootNode = Helper(container,rootNode,0);
            BatchTreeNode(rootNode,info,instanceData);
            if (onProgress != null)
                onProgress(0.0f);
            InstanceData data = GetInstanceData(instanceData);
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
            defaultController.usePreciseCulling = usePreciseCulling;
        }
        private InstanceData GetInstanceData(AllInstanceData instanceData)
        {
            InstanceData tempData = new InstanceData();
            tempData.m_materials = instanceData.Materials;
            tempData.m_meshs = instanceData.Meshes;
            tempData.m_renderClass = instanceData.RenderClassStates;
            tempData.transforms = instanceData.GetMatrix4X4s();
            tempData.m_LightMapIndexsData = instanceData.GetLightMapIndexs();
            tempData.m_LightMapOffsetsData = instanceData.GetLigthMapOffests();
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
                instanceTreeNode.HighObjects = highObj;
            }

            foreach (var info in infos)
            {
                var instanceTreeNode = convertedTable[info.Target];
                var LowNodeclassificationObject = info.classificationObjects;
                var lowObject =  GetNodeData(LowNodeclassificationObject,datas);
                instanceTreeNode.LowObjects = lowObject;
                instanceTreeNode.LowCullBounds = info.CalculateRealBound();
            }
        }
        /// <summary>
        /// 从分类好的OBJ中构建NodeData，按照Mesh和材质分类
        /// </summary>
        /// <param name="classificationObjects"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private List<NodeData> GetNodeData(Dictionary<long, NodeObject> classificationObjects,AllInstanceData datas)
        {
            List<NodeData> result = new List<NodeData>();
            foreach (var nodePair in classificationObjects)
            {
                List<int> minGameObjectsIndex = new List<int>();
                //每种类型（材质加mesh）下的OBJList
                var nodelist = nodePair.Value;
                int meshIndex = datas.GetAssetIndex(nodePair.Value.m_mesh);
                if (meshIndex == -1)
                {
                    Debug.Log("index = -1");
                }
                int matIndex = datas.GetAssetIndex(nodePair.Value.m_mat);
                int subMesh = nodePair.Value.subMeshIndex;
                bool needLighMap = nodePair.Value.m_lightMode == LightMode.LightMap;
                //收集所有的矩阵信息
                foreach (var nodeObject in nodelist.m_gameobjs)
                {
                    int index = datas.GetAssetIndex(nodeObject);
                    minGameObjectsIndex.Add(index);
                }
                minGameObjectsIndex.Sort();
                var matrixs = minGameObjectsIndex.SplitArrayIntoConsecutiveSubArrays((a, b) =>
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
                // ----------------------------------- 
                NodeData tempNodteData = new NodeData();
                tempNodteData.m_RenderData = matrixs;
                tempNodteData.m_material = matIndex;
                tempNodteData.m_meshIndex = meshIndex;
                tempNodteData.subMeshIndex = subMesh;
                tempNodteData.m_castShadow = nodePair.Value.m_castShadow;
                tempNodteData.m_queue = nodePair.Value.m_queue;
                tempNodteData.m_identifier = nodePair.Value.Identifier;
                tempNodteData.m_NeedLightMap = needLighMap;
                result.Add(tempNodteData);
            }

           
            return result;
        }
        Dictionary<SpaceNode, InstanceTreeNode> convertedTable = new Dictionary<SpaceNode, InstanceTreeNode>();
        
        public int CountSubtrees(SpaceNode root)
        {
            int count = 1; // 包括当前节点本身的数量
            for (int i = 0; i < root.GetChildCount(); i++)
            {
                count += CountSubtrees(root.GetChild(i));
            }
            return count;
        }
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
            while (instanceTreeNodes.Count > 0)
            {
                var instanceTreeNode = instanceTreeNodes.Dequeue();
                var spaceNode = spaceNodes.Dequeue();
                int level = levels.Dequeue();

                convertedTable[spaceNode] = instanceTreeNode;
                instanceTreeNode.Level = level;
                instanceTreeNode.Bounds = spaceNode.Bounds;
                instanceTreeNode.HighCullBounds = spaceNode.CalculateRealBound();
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

        private InstanceTreeNode Helper(InstanceTreeNodeContainer container, SpaceNode rootNode,int level)
        {
            InstanceTreeNode root = new InstanceTreeNode();
            convertedTable[rootNode] = root;
            root.Level = level;
            root.Bounds = rootNode.Bounds;
            root.SetContainer(container);
            root.HighCullBounds = rootNode.CalculateRealBound();
            root.ChildNumber = CountSubtrees(rootNode)- 1;
            for (int i = 0; i < rootNode.GetChildCount(); i++)
            {
                root.SetChildTreeNode(Helper(container,rootNode.GetChild(i),level+1));
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