using UnityEngine;

namespace Unity.MergeInstancingSystem.SpaceManager
{
    public interface ISpaceManager
    {
        void UpdateCamera(Transform instanceTransform, Camera cam);
        bool IsHigh(float lodDistance, Bounds bounds);
        /// <summary>
        /// 判断一个包围盒是否要被剔除
        /// </summary>
        /// <param name="cullDistance"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        bool IsCull(float cullDistance, Bounds bounds);

        bool CompletelyCull(Bounds bounds,out bool isCompletely);
        /// <summary>
        /// 计算包围盒中心与相机的距离的平方，在xz平面
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        float GetDistanceSqure(Bounds bounds);
    }
}