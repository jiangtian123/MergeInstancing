using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.Pool
{
    public class PoolManager
    {
        
        private static Dictionary<int,object> m_pools = new Dictionary<int,object>();
        private static int IUid = 0;

        private static  PoolManager m_instance;

        public static PoolManager Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new PoolManager();
                }
                return m_instance;
            }
        }

        #region Allocat
        public int AllocatPool<T>(int poolCount) where T : unmanaged
        {
            int id = IUid++;
            PoolItem<T> temp = new PoolItem<T>(id, poolCount, typeof(int));
            m_pools.Add(id,temp);
            return id;
        }
        #endregion

        #region AddData

        public void AddData<T>(int poolID,int index,ref T[] element)where T : unmanaged
        {
            Profiler.BeginSample("Begin Add Pool");
            var pool = (PoolItem<T>)m_pools[poolID];
            pool.Add(index,element);
            Profiler.EndSample();
        }
        public void CopyData<T>(int poolID,T[] source,int head,int length)where T : unmanaged
        {
            try
            {
                var pool = (PoolItem<T>)m_pools[poolID];
                pool.CopyToArray(source,head,length);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
           
        }
        #endregion

        #region GetData

        public int GetPoolCount<T>(int poolId) where T : unmanaged
        {
            if (poolId == -1)
            {
                return 0;
            }
            return ((PoolItem<T>)m_pools[poolId]).Count;
        }
        public List<Pool<T>>GetData<T>(int poolId)where T : unmanaged
        {
            try
            {
                return ((PoolItem<T>)m_pools[poolId]).GetArray();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
        #endregion
        public void ResetPool<T>(int id)where T : unmanaged
        {
            try
            {
                ((PoolItem<T>)m_pools[id]).Reset();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            
        }
        public void ReleasePool<T>(int id)where T : unmanaged
        {
            if (id == -1)
            {
                return;
            }
            if (m_pools.ContainsKey(id))
            {
                ((PoolItem<T>)m_pools[id]).Destory();
                m_pools.Remove(id);
            }
        }
       
    }
}