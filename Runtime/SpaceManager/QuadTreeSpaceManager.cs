using System.Collections.Generic;
using UnityEngine;

namespace Unity.MergeInstancingSystem.SpaceManager
{
    public class QuadTreeSpaceManager : ISpaceManager
    {
        private float preRelative;
        /// <summary>
        /// 根节点下相机的空间
        /// </summary>
        private Vector3 camPosition;

        private Camera m_camera;
        public QuadTreeSpaceManager()
        {
        }
        /// <summary>
        /// 将相机转到根节点所在的空间中
        /// </summary>
        /// <param name="hlodTransform"></param>
        /// <param name="cam"></param>
        public void UpdateCamera(Transform hlodTransform, Camera cam)
        {
            if (cam.orthographic)
            {
                preRelative = 0.5f / cam.orthographicSize;
            }
            else
            {
                float halfAngle = Mathf.Tan(Mathf.Deg2Rad * cam.fieldOfView * 0.5F);
                preRelative = 0.5f / halfAngle;
            }
            preRelative = preRelative * QualitySettings.lodBias;
            camPosition = cam.transform.position;
            m_camera = cam;
        }
        /// <summary>
        /// 判断是否为高等级显示
        /// </summary>
        /// <param name="lodDistance"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public bool IsHigh(float lodDistance, Bounds bounds)
        {
            //float distance = 1.0f;
            //if (cam.orthographic == false)
            
            float distance = GetDistance(bounds.center, camPosition);
            float relativeHeight = bounds.size.x * preRelative*0.5f / distance;
            return relativeHeight > lodDistance;
        }
        /// <summary>
        /// 计算包围盒中心与相机的距离的平方
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public float GetDistanceSqure(Bounds bounds)
        {
            float x = bounds.center.x - camPosition.x;
            float z = bounds.center.z - camPosition.z;

            float square = x * x + z * z;
            return square;
        }
        /// <summary>
        /// 判断当前包围盒应不应该被剔除，包围盒距离相机有多远为依据，足够远就会被剔除
        /// </summary>
        /// <param name="cullDistance">小于这个值就会被剔除</param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public bool IsCull(float cullDistance, Bounds bounds)
        {
            var planes =  GeometryUtility.CalculateFrustumPlanes(m_camera);
            if (!GeometryUtility.TestPlanesAABB(planes,bounds))
            {
                return true;
            }
            
            float camer_farDis = m_camera.farClipPlane;
            float distance = GetDistance(bounds.center, camPosition);
            float bais = bounds.size.x * preRelative;
            float relativeHeight = distance / (camer_farDis + bais);
            return relativeHeight > 1 - cullDistance;
        }
        /// <summary>
        /// 如果包围盒有一个顶点在视锥体的外面就返回false
        /// </summary>
        /// <param name="box"></param>
        /// <returns></returns>
        public bool IsBOXInsideViewFrustum(Bounds box)
        {
            var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(m_camera);
            List<Vector3> points = new List<Vector3>();
            Vector3 min =box.min;
            Vector3 max = box.max;
            points.Add(new Vector3(min.x, min.y, min.z));
            points.Add(new Vector3(min.x, min.y, max.z));
            points.Add( new Vector3(max.x, min.y, max.z));
            points.Add( new Vector3(max.x, min.y, min.z));

            points.Add(new Vector3(min.x, max.y, min.z));
            points.Add(new Vector3(min.x, max.y, max.z));
            points.Add(new Vector3(max.x, max.y, max.z));
            points.Add(new Vector3(max.x, max.y, min.z));
            foreach (var point in points)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (!frustumPlanes[i].GetSide(point))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        /// <summary>
        /// 计算包围盒和相机在xz平面上的距离
        /// </summary>
        /// <param name="boundsPos"></param>
        /// <param name="camPos"></param>
        /// <returns></returns>
        private float GetDistance(Vector3 boundsPos, Vector3 camPos)
        {
            float x = boundsPos.x - camPos.x;
            float z = boundsPos.z - camPos.z;
            float square = x * x + z * z;
            return Mathf.Sqrt(square);
        }
    }
}