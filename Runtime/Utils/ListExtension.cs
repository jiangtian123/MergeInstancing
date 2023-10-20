using System;
using System.Collections;
using System.Collections.Generic;

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
        /// <summary>
        /// 将一个int类型的数组分割成多个连续的
        /// </summary>
        /// <param name="indexList"></param>
        /// <returns></returns>
        public  static List<NodeData.ListInfo> SplitArrayIntoConsecutiveSubArrays(this List<int> indexList,Func<int,int,bool> onProgress)
        {
            List<NodeData.ListInfo> result = new List<NodeData.ListInfo>();
           int[] sortedArr = indexList.ToArray();
            int start = 0;
            int end = 0;
            while (end < indexList.Count)
            {
                int count = 1;
                while (end + 1 < indexList.Count &&  onProgress(indexList[end + 1],indexList[end]))
                {
                    count++;
                    end++;
                }
                NodeData.ListInfo temp = new NodeData.ListInfo();
                temp.head = indexList[start];
                temp.length = count;
                result.Add(temp);
                start = end + 1;
                end = start;
            }
            return result;
        }
    }
}