using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.MergeInstancingSystem.CustomData
{
   
   
   [Serializable]
   public struct DPlane : IEquatable<DPlane>
   {
      private float4 m_NormalDist;

      public float4 normalDist { get { return m_NormalDist; } set { m_NormalDist = value; } }


      public DPlane(float3 inNormal, float3 inPoint)
      {
         m_NormalDist = new float4(1, 1, 1, 1);
         m_NormalDist.xyz = math.normalize(inNormal);
         m_NormalDist.w = -math.dot(m_NormalDist.xyz, inPoint);
      }

      public DPlane(float3 inNormal, float d)
      {
         m_NormalDist = new float4(1, 1, 1, 1);
         m_NormalDist.xyz = math.normalize(inNormal);
         m_NormalDist.w = d;
      }

      public DPlane(float3 a, float3 b, float3 c)
      {
         m_NormalDist = new float4(1, 1, 1, 1);
         m_NormalDist.xyz = math.normalize(math.cross(b - a, c - a));
         m_NormalDist.w = -math.dot(m_NormalDist.xyz, a);
      }

      public bool GetSide(float3 point)
      {
         return (math.dot(m_NormalDist.xyz, point) + m_NormalDist.w) >= 0 ? true : false;
      }
      
      public override bool Equals(object other)
      {
         if (!(other is DPlane)) return false;

         return Equals((DPlane)other);
      }

      public bool Equals(DPlane other)
      {
         return normalDist.Equals(other.normalDist);
      }

      public override int GetHashCode()
      {
         return m_NormalDist.GetHashCode();
      }

      public static implicit operator Plane(DPlane plane) { return new Plane(plane.normalDist.xyz, plane.normalDist.w); }

      public static implicit operator DPlane(Plane plane) { return new DPlane(plane.normal, plane.distance); }
   }
   
   [Serializable]
   public struct DAABB : IEquatable<DAABB>
   {
      [SerializeField] private float3 m_Center;
      [SerializeField] private float3 m_Extents;
      public float3 center { get { return m_Center; } set { m_Center = value; } }
      
      public float3 size { get { return m_Extents * 2.0F; } set { m_Extents = value * 0.5F; } }
      public float3 extents { get { return m_Extents; } set { m_Extents = value; } }
      public float3 min { get { return center - extents; } set { SetMinMax(value, max); } }
      public float3 max { get { return center + extents; } set { SetMinMax(min, value); } }
      
      public DAABB(float3 center, float3 size)
      {
         m_Center = center;
         m_Extents = size * 0.5F;
      }
      public void SetMinMax(in float3 min, in float3 max)
      {
         extents = (max - min) * 0.5F;
         center = min + extents;
      }
      
      public override bool Equals(object other)
      {
         if (!(other is DAABB)) return false;

         return Equals((DAABB)other);
      }

      public bool Equals(DAABB other)
      {
         return center.Equals(other.center) && extents.Equals(other.extents);
      }
      
      public override int GetHashCode()
      {
         return center.GetHashCode() ^ (extents.GetHashCode() << 2);
      }
      
      //-------- 定义两个隐式类型转换 -------------
      public static implicit operator Bounds(DAABB AABB) { return new Bounds(AABB.center, AABB.size); }

      public static implicit operator DAABB(Bounds Bound) { return new DAABB(Bound.center, Bound.size); }
   }

   public static class Geometry
   {
      
      /// <summary>
      /// 计算世界坐标下的包围盒，直接展开成内联函数
      /// </summary>
      /// <param name="bound"></param>
      /// <param name="matrix"></param>
      /// <returns></returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static DAABB CaculateWorldBound(in DAABB bound, in Matrix4x4 matrix)
      {
         float4 center = matrix * new float4(bound.center.x, bound.center.y, bound.center.z, 1);
         float4 extents = math.abs(matrix.GetColumn(0) * bound.extents.x) + math.abs(matrix.GetColumn(1) * bound.extents.y) + math.abs(matrix.GetColumn(2) * bound.extents.z);
         return new DAABB(center.xyz, extents.xyz * 2);
      }
      
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static float Squared(in float a)
      {
         return a * a;
      }
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static float GetBoxLength(ref DAABB box)
      {
         float a = box.extents.x;
         float b = box.extents.z;
         return math.sqrt(Squared(a) + Squared(b ));
      }
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static float GetDistance(float3 cameraPos,float3 boxPos)
      {
         return math.sqrt((Squared(cameraPos.x - boxPos.x) + Squared(cameraPos.z - boxPos.z)));
      }
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool IntersectAABBFrustum(in DAABB bound,in NativeArray<DPlane> planes,out bool isCompletely)
      {
         isCompletely = false;
         float3 min = bound.min;
         float3 max = bound.max;
         float3[] points = new float3[8];
         points[0] = new float3(min.x, min.y, min.z);
         points[1] = new float3(min.x, min.y, max.z);
         points[2] = new float3(max.x, min.y, max.z);
         points[3] = new float3(max.x, min.y, min.z);

         points[4] = new float3(min.x, max.y, min.z);
         points[5] = new float3(min.x, max.y, max.z);
         points[6] = new float3(max.x, max.y, max.z);
         points[7] = new float3(max.x, max.y, min.z);
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
               return false;
            }
         }
         return true;
      }
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool GetPlaneSide(ref DPlane plane,in float3 point)
      {
         return (math.dot(plane.normalDist.xyz, point) + plane.normalDist.w) >= 0 ? true : false;
      }
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool IsHigh(float lodDistance,in DAABB bounds,in float preRelative,in float3 camPosition)
      {
         //float distance = 1.0f;
         //if (cam.orthographic == false)
            
         float distance = GetDistance(bounds.center, camPosition);
         float relativeHeight = bounds.size.x * preRelative*0.5f / distance;
         return relativeHeight > lodDistance;
      }
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool DisCull(float3 camPosition,ref DAABB bounds,float preRelative,float cullDistance)
      {
         float distance = GetDistance(bounds.center, camPosition);
         float relativeHeight = bounds.size.x * preRelative / distance;
         return relativeHeight < cullDistance;
      }
   }
}