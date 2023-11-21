using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.MergeInstancingSystem.Utils
{
    public static class ListExtension
    {
        public static void RemoveAll<T>(this List<T> list, IEnumerable<T> removeList)
        {
            foreach (var item in removeList)
            {
                list.Remove(item);
            }
        }
        public static void AddLikeHashSet<T>(this List<T> array,T value)
        {
            if (!array.Contains(value))
            {
                array.Add(value);
            }
        }

        public unsafe static void CopyTo<T>(this T[] array, NativeArray<T> source, int count)  where T : unmanaged
        {
            if (!source.IsCreated || array == null)
            {
                return;
            }

            fixed (void* destiantionPtr = array)
            {
                Buffer.MemoryCopy(source.GetUnsafePtr(),destiantionPtr,0,count);
            }
        }
    }
}