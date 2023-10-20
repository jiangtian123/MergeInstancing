using System;
using System.Collections.Generic;
using UnityEngine;
namespace Unity.MergeInstancingSystem.SpaceManager
{
    public interface ISpaceSplitter
    {
        /// <summary>
        /// 计算子树的数量
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        int CalculateSubTreeCount(Bounds bounds);
        /// <summary>
        /// 计算树的深度
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="chunkSize"></param>
        /// <returns></returns>
        int CalculateTreeDepth(Bounds bounds, float chunkSize);
        
        /// <summary>
        /// 将包围盒划分成四叉树
        /// </summary>
        /// <param name="initBounds">在根节点空间下的包含所有Renderer的包围盒</param>
        /// <param name="chunkSize">最小的节点的尺寸</param>
        /// <param name="transform">根节点的Transform</param>
        /// <param name="targetObjects">跟节点下所有的实例</param>
        /// <param name="onProgress">显示处理进度的回调函数</param>
        /// <returns></returns>
        List<SpaceNode> CreateSpaceTree(Bounds initBounds, float chunkSize, Transform transform, List<GameObject> targetObjects, Action<float> onProgress);
    }
}