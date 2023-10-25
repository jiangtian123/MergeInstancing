using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Unity.MergeInstancingSystem.SpaceManager
{
    public static class BoundsExtension
    {
        /// <summary>
        /// 判断两个包围盒所属关系
        /// </summary>
        /// <param name="bounds">物体的包围盒</param>
        /// <param name="target">四叉树的包围盒</param>
        /// <returns></returns>
        public static bool IsPartOf(this Bounds bounds, Bounds target)
        {
            float dis_x = Mathf.Abs(bounds.center.x - target.center.x);
            float dis_y = Mathf.Abs(bounds.center.y - target.center.y);
            float dis_z = Mathf.Abs(bounds.center.z - target.center.z);
            //物体包围盒的中心在四叉树包围盒内
            bool centerInBox = dis_x < target.extents.x && dis_y < target.extents.y && dis_z < target.extents.z;
            //target 小于 bounds 为true
            bool isInBOX = (double)bounds.size.x <= (double)target.size.x &&
                           (double)bounds.size.y <= (double)target.size.y &&
                           (double)bounds.size.z <= (double)target.size.z;
            return centerInBox && isInBOX;
            // bool isInBOX = (double)bounds.min.x >= (double)target.min.x &&
            //                (double)bounds.max.x <= (double)target.max.x &&
            //                (double)bounds.min.y >= (double)target.min.y &&
            //                (double)bounds.max.y <= (double)target.max.y &&
            //                (double)bounds.min.z >= (double)target.min.z &&
            //                (double)bounds.max.z <= (double)target.max.z;
            // return isInBOX;
        }
    }
    public class QuadTreeSpaceSplitter : ISpaceSplitter
    {
        /// <summary>
        /// 将该方法标记为编辑器自动加载的方法
        /// </summary>
        [InitializeOnLoadMethod]
        static void RegisterType()
        {
            SpaceSplitterTypes.RegisterSpaceSplitterType(typeof(QuadTreeSpaceSplitter));
        }
        
        private float m_looseSizeFromOptions;
        
        private bool m_useSubHLODTree;
        
        private float m_subHLODTreeSize;
        
        
        
        public QuadTreeSpaceSplitter(SerializableDynamicObject spaceSplitterOptions)
        {
            m_looseSizeFromOptions = 0.0f;

            m_useSubHLODTree = false;
            m_subHLODTreeSize = 0.0f;
            
            if (spaceSplitterOptions == null)
            {
                return;
            }
            
            dynamic options = spaceSplitterOptions;
            if(options.LooseSize1 != null)
                m_looseSizeFromOptions = options.LooseSize;
            if(options.UseSubHLODTree != null)
                m_useSubHLODTree = options.UseSubHLODTree;
            if(options.SubHLODTreeSize != null)
                m_subHLODTreeSize = options.SubHLODTreeSize;
            
        }
        /// <summary>
        /// 目标物体的描述结构，包含一个OBJ和一个Bounds
        /// </summary>
        struct TargetInfo
        {
            public GameObject GameObject;
            public Bounds Bounds;
        }
        /// <summary>
        /// 构建四叉树
        /// </summary>
        /// <param name="initBounds">包含所有OBJ的包围盒</param>
        /// <param name="chunkSize">包围盒最小的大小</param>
        /// <param name="transform"></param>
        /// <param name="targetObjects">所有OBJ</param>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public List<SpaceNode> CreateSpaceTree(Bounds initBounds, float chunkSize, Transform transform,
            List<GameObject> targetObjects, Action<float> onProgress)
        {
            List<SpaceNode> nodes = new List<SpaceNode>();
            List<TargetInfo> targetInfos = CreateTargetInfoList(targetObjects, transform);
            if (m_useSubHLODTree)
            {
                //先将空间按照子树的大小划分一下
                List<Bounds> splittedBounds = SplitBounds(initBounds, m_subHLODTreeSize);
                //将所有的实例放到它所属的子树中
                List<List<TargetInfo>> splittedTargetInfos = SplitTargetObjects(targetInfos, splittedBounds);
                float progressSize = 1.0f / splittedTargetInfos.Count; 
                //遍历所有的子树
                for (int i = 0; i < splittedTargetInfos.Count; ++i)
                {
                    nodes.Add(CreateSpaceTreeImpl(splittedBounds[i], chunkSize, splittedTargetInfos[i], (p =>
                    {
                        float startProgress = i * progressSize;
                        onProgress?.Invoke(startProgress + p * progressSize);
                    })));
                }
            }
            else
            {
                nodes.Add(CreateSpaceTreeImpl(initBounds, chunkSize, targetInfos, onProgress));
            }

            return nodes;
        }
        private float CalcLooseSize(float chunkSize)
        {
            //If the chunk size is small, there is a problem that it may get caught in an infinite loop.
            //So, the size can be determined according to the chunk size.
            return Mathf.Min(chunkSize * 0.3f, m_looseSizeFromOptions);
            
        }
        /// <summary>
        /// 将一个包围盒划分成四叉树
        /// </summary>
        /// <param name="initBounds">要划分的包围盒</param>
        /// <param name="chunkSize">叶子节点的大小</param>
        /// <param name="targetObjects">属于这个包围盒的实例</param>
        /// <param name="onProgress">划分进程回调函数</param>
        /// <returns></returns>
        private SpaceNode CreateSpaceTreeImpl(Bounds initBounds, float chunkSize, List<TargetInfo> targetObjects,
            Action<float> onProgress)
        {
            float looseSize = CalcLooseSize(chunkSize);
            SpaceNode rootNode = new SpaceNode();
            rootNode.Bounds = initBounds;

            if ( onProgress != null)
                onProgress(0.0f);

			//space split first
			Stack<SpaceNode> nodeStack = new Stack<SpaceNode>();
			nodeStack.Push(rootNode);
            //切割一个大块，并且递归的切割每一个超过规定大小的块
			while(nodeStack.Count > 0 )
			{
				SpaceNode node = nodeStack.Pop();
                //如果当前节点的包围盒的x轴的长度大于设置好的每块的长度就进行分割
				if ( node.Bounds.size.x > chunkSize )
				{
                    List<SpaceNode> childNodes = CreateChildSpaceNodes(node, looseSize);
					
					for ( int i = 0; i < childNodes.Count; ++i )
                    {
                        //设置父物体
                        childNodes[i].ParentNode = node;
						nodeStack.Push(childNodes[i]);
					}
						
				}
			}
            //如果没有目标物体就返回了
            if (targetObjects == null)
                return rootNode;

            for (int oi = 0; oi < targetObjects.Count; ++oi)
            {
                Bounds objectBounds = targetObjects[oi].Bounds;
                SpaceNode target = rootNode;
                //将每个物体放到最适合的它的位置
                while (true)
                {
                    if (target.HasChild())
                    {
                        //the object can be in the over 2 nodes.
                        //we should figure out which node is more close with the object.
                        int nearestChild = -1;
                        float nearestDistance = float.MaxValue;
                        for (int ci = 0; ci < target.GetChildCount(); ++ci)
                        {
                            if (objectBounds.IsPartOf(target.GetChild(ci).Bounds))
                            {
                                float dist = Vector3.Distance(target.GetChild(ci).Bounds.center, objectBounds.center);

                                if (dist < nearestDistance)
                                {
                                    nearestChild = ci;
                                    nearestDistance = dist;
                                }
                            }
                        }

                        //We should find out it until we get the fit size from the bottom.
                        //this means the object is small to add in the current node.
                        if (nearestChild >= 0)
                        {
                            target = target.GetChild(nearestChild);
                            continue;
                        }
                    }
                    target.Objects.Add(targetObjects[oi].GameObject);
                    break;
                }        
                
                if ( onProgress != null)
                    onProgress((float)oi/ (float)targetObjects.Count);
            }
            
            return rootNode;
        }
        
        
        /// <summary>
        /// 遍历所有的对象，然后计算其包围盒（包含所有renderer的包围盒）在HLODS的根节点空间下
        /// </summary>
        /// <param name="gameObjects">根节点</param>
        /// <param name="transform"></param>
        /// <returns></returns>
        private List<TargetInfo> CreateTargetInfoList(List<GameObject> gameObjects, Transform transform)
        {
            List<TargetInfo> targetInfos = new List<TargetInfo>(gameObjects.Count);

            for (int i = 0; i < gameObjects.Count; ++i)
            {
                Bounds? bounds = CalculateBounds(gameObjects[i], transform);
                if ( bounds == null )
                    continue;
                targetInfos.Add(new TargetInfo()
                {
                    GameObject = gameObjects[i],
                    Bounds = bounds.Value,
                });
            }

            return targetInfos;
        }
        
        
        public int CalculateSubTreeCount(Bounds bounds)
        {
            if (m_useSubHLODTree == false)
                return 1;
            
            List<Bounds> splittedBounds = SplitBounds(bounds, m_subHLODTreeSize);
            return splittedBounds.Count;
        }
        
        /// <summary>
        /// 计算四叉树的深度
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="chunkSize"></param>
        /// <returns></returns>
        public int CalculateTreeDepth(Bounds bounds, float chunkSize)
        {
            float maxLength = 0.0f;
            if (m_useSubHLODTree)
            {
                List<Bounds> splittedBounds = SplitBounds(bounds, m_subHLODTreeSize);
                if (splittedBounds.Count > 0)
                {
                    maxLength = Mathf.Max(splittedBounds[0].extents.x, splittedBounds[0].extents.z);
                }
                else
                {
                    maxLength = Mathf.Max(bounds.extents.x, bounds.extents.z);    
                }
            }
            else
            {
                maxLength = Mathf.Max(bounds.extents.x, bounds.extents.z);
            }

            int depth = 1;

            while (maxLength > chunkSize)
            {
                depth += 1;
                maxLength *= 0.5f;
            }

            return depth;
        }
        
        /// <summary>
        /// 将一个OBJ的包围盒转到HLODS根节点空间下后求出一个最大的包围盒
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="transform"></param>
        /// <returns></returns>
        private Bounds? CalculateBounds(GameObject obj, Transform transform)
        {
            MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
                return null;

            Bounds result = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; ++i)
            {
                result.Encapsulate(renderers[i].bounds);
            }
            return result;
        }
        /// <summary>
        /// 将一个包围盒按照XZ轴划分
        /// </summary>
        /// <param name="bounds">要划分的包围盒</param>
        /// <param name="splitSize">每块的大小</param>
        /// <returns></returns>
        private List<Bounds> SplitBounds(Bounds bounds, float splitSize)
        {
            int xcount = Mathf.CeilToInt(bounds.size.x / splitSize);
            int zcount = Mathf.CeilToInt(bounds.size.z / splitSize);

            float xsize = bounds.size.x / xcount;
            float zsize = bounds.size.z / zcount;

            List<Bounds> boundsList = new List<Bounds>();
            Vector3 splitBoundSize = new Vector3(xsize, bounds.size.y, zsize);
            
            for (int z = 0; z < zcount; ++z)
            {
                for (int x = 0; x < xcount; ++x)
                {
                    Vector3 center = new Vector3(
                        x * xsize + xsize * 0.5f,
                        bounds.extents.y,
                        z * zsize + zsize * 0.5f) + bounds.min;
                    
                    boundsList.Add(new Bounds(center,splitBoundSize));
                }
            }

            return boundsList;
        }
        /// <summary>
        /// 将一个大块分割成4个小块，会扩张一下X.Z轴
        /// </summary>
        /// <param name="parentNode">需要分割的快</param>
        /// <param name="looseSize">扩张多少</param>
        /// <returns></returns>
        private List<SpaceNode> CreateChildSpaceNodes(SpaceNode parentNode, float looseSize)
        {
            //------------ 将包围盒改成按原来包围盒的形式划分 ---------------------------------
            List<SpaceNode> childSpaceNodes = new List<SpaceNode>(4);
            
            float x_size = parentNode.Bounds.size.x;
            float y_size = parentNode.Bounds.size.y;
            float z_size = parentNode.Bounds.size.z;
            float x_extend = x_size * 0.5f;
            float z_extend = z_size * 0.5f;
            float x_offset = x_extend * 0.5f;
            float z_offset = z_extend * 0.5f;
            Vector3 center = parentNode.Bounds.center;
            Vector3 looseBoundsSize = new Vector3(x_extend + looseSize, y_size, z_extend + looseSize);

            childSpaceNodes.Add(
                SpaceNode.CreateSpaceNodeWithBounds(
                    new Bounds(center + new Vector3(-x_offset, 0.0f, -z_offset), looseBoundsSize)
                ));
            childSpaceNodes.Add(
                SpaceNode.CreateSpaceNodeWithBounds(
                    new Bounds(center + new Vector3(-x_offset, 0.0f, z_offset), looseBoundsSize)
                ));
            childSpaceNodes.Add(
                SpaceNode.CreateSpaceNodeWithBounds(
                    new Bounds(center + new Vector3(x_offset, 0.0f, -z_offset), looseBoundsSize)
                ));
            childSpaceNodes.Add(
                SpaceNode.CreateSpaceNodeWithBounds(
                    new Bounds(center + new Vector3(x_offset, 0.0f, z_offset), looseBoundsSize)
                ));
            
            return childSpaceNodes;
        }
        /// <summary>
        /// 判断所有的实例属于哪个子树
        /// </summary>
        /// <param name="targetInfoList">包含所有Renderer实例的信息</param>
        /// <param name="targetBoundList">空间包围盒</param>
        /// <returns></returns>
        private List<List<TargetInfo>> SplitTargetObjects(List<TargetInfo> targetInfoList, List<Bounds> targetBoundList)
        {
            List<List<TargetInfo>> targetObjectsList = new List<List<TargetInfo>>();
            //划分了几个空间，就有几个
            for (int i = 0; i < targetBoundList.Count; ++i)
            {
                targetObjectsList.Add(new List<TargetInfo>());
            }
            //遍历每个renderer实例，放到最合适的包围盒中
            foreach (var targetInfo in targetInfoList)
            {
                for (int bi = 0; bi < targetBoundList.Count; ++bi)
                {
                    if (targetBoundList[bi].Contains(targetInfo.Bounds.center))
                    {
                        targetObjectsList[bi].Add(targetInfo);
                        break;
                    }
                }
            }

            return targetObjectsList;
        }
        
    }
}