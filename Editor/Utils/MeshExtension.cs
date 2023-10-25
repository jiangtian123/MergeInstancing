using System.Collections.Generic;
using UnityEngine;

namespace Unity.MergeInstancingSystem.Utils
{
    public static class MeshExtension
    {
        /// <summary>
        /// 一个Mesh可以分多个MinGameObject
        /// </summary>
        /// <param name="meshRenderer"></param>
        /// <returns></returns>
        public static List<MinGameObject> MeshRenderToMinGameObjects(this MeshRenderer meshRenderer)
        {
            List<MinGameObject> result = new List<MinGameObject>();
            return result;
        }
    }
}