using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

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

        private List<Vector3> points;
        public QuadTreeSpaceManager()
        {
            points = new List<Vector3>(8);
            for (int i = 0; i < 8; i++)
            {
                points.Add(new Vector3());
            }
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
            Profiler.BeginSample("Culling");
            float distance = GetDistance(bounds.center, camPosition);
            //bound.size是包围盒的长宽高，包围盒是个立方体
            // preRelative 是 视锥体一半的 0.5/tanΘ。
            // bounds.size.x * preRelative = y * 0.5 / tanΘ * bais
            //bais越大，越不容易被剔除。
            float relativeHeight = bounds.size.x * preRelative / distance;
            Profiler.EndSample();
            return relativeHeight < cullDistance;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="isCompletely">是否需要逐个剔除</param>
        /// <returns>true 就是在视锥体内</returns>
        public bool CompletelyCull(Bounds bounds, out bool isCompletely)
        { 
            Profiler.BeginSample("Completely Cull");
            var planes =  CameraRecognizerManager.ActiveRecognizer.planes;
            isCompletely = false;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            if (min == max)
            {
                Profiler.EndSample();
                return false;
            }
            points[0] = new Vector3(min.x, min.y, min.z);
            points[1] = new Vector3(min.x, min.y, max.z);
            points[2] = new Vector3(max.x, min.y, max.z);
            points[3] = new Vector3(max.x, min.y, min.z);

            points[4] = new Vector3(min.x, max.y, min.z);
            points[5] = new Vector3(min.x, max.y, max.z);
            points[6] = new Vector3(max.x, max.y, max.z);
            points[7] = new Vector3(max.x, max.y, min.z);
            for(int p = 0; p < (int)planes.Length; ++p)
            {
                bool inside = false;
                for(int c = 0; c < 8; ++c)
                {
                    //用包围盒8个点判断
                    //只要有一个点在这个面里面，就不判断了
                    if(planes[p].GetSide(points[c]))
                    {
                        inside = true;
                        break;
                    }
                    isCompletely = true;
                }
                //所有顶点都在包围盒外，被剔除。
                if(!inside)
                {
                    isCompletely = true;
                    Profiler.EndSample();
                    return false;
                }
            }
            Profiler.EndSample();
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