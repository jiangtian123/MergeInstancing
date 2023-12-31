﻿using System.Collections.Generic;
using UnityEngine;
namespace Unity.MergeInstancingSystem.Utils
{
    public class BoundsUtils
    {
        /// <summary>
        /// 将包围盒转到Transform所在的空间后重新计算
        /// </summary>
        /// <param name="renderer">附带包围盒的renderer</param>
        /// <param name="transform"></param>
        /// <returns>返回的是物体坐标空间下的AABB包围盒</returns>
        public static Bounds CalcLocalBounds(Bounds localBounds, Matrix4x4 localToWorld)
        {
            Bounds bounds = localBounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Matrix4x4 matrix = localToWorld;

            Vector3[] points = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z),
            };

            for (int i = 0; i < points.Length; ++i)
            {
                points[i] = matrix.MultiplyPoint(points[i]);
            }

            Vector3 newMin = points[0];
            Vector3 newMax = points[0];

            for (int i = 1; i < points.Length; ++i)
            {
                if (newMin.x > points[i].x) newMin.x = points[i].x;
                if (newMax.x < points[i].x) newMax.x = points[i].x;
                
                if (newMin.y > points[i].y) newMin.y = points[i].y;
                if (newMax.y < points[i].y) newMax.y = points[i].y;
                
                if (newMin.z > points[i].z) newMin.z = points[i].z;
                if (newMax.z < points[i].z) newMax.z = points[i].z;
            }


            Bounds newBounds = new Bounds();
            newBounds.SetMinMax(newMin, newMax);
            return newBounds;
        }
        
    }
}