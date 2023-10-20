using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.Pool
{
    public class PoolManager
    {
        private static Dictionary<int,BasePool> m_pools = new Dictionary<int,BasePool>();
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
        public int AllocatIntPool(int poolCount)
        {
            int id = IUid++;
            PoolItem<int> temp = new PoolItem<int>(id, poolCount, typeof(int));
            m_pools.Add(id,temp);
            return id;
        }
        public int AllocatFloatPool(int poolCount)
        {
            int id = IUid++;
            PoolItem<float> temp = new PoolItem<float>(id, poolCount, typeof(float));
            m_pools.Add(id,temp);
            return id;
        }
        public int AllocatMatrix4x4Pool(int poolCount)
        {
            int id = IUid++;
            PoolItem<Matrix4x4> temp = new PoolItem<Matrix4x4>(id, poolCount, typeof(Matrix4x4));
            m_pools.Add(id,temp);
            return id;
        }
        public int AllocatStringPool(int poolCount)
        {
            int id = IUid++;
            PoolItem<string> temp = new PoolItem<string>(id, poolCount, typeof(string));
            m_pools.Add(id,temp);
            return id;
        }
        public int AllocatVector2Pool(int poolCount)
        {
            int id = IUid++;
            PoolItem<Vector2> temp = new PoolItem<Vector2>(id, poolCount, typeof(Vector2));
            m_pools.Add(id,temp);
            return id;
        }
        public int AllocatVector3Pool(int poolCount)
        {
            int id = IUid++;
            PoolItem<Vector3> temp = new PoolItem<Vector3>(id, poolCount, typeof(Vector3));
            m_pools.Add(id,temp);
            return id;
        }
        public int AllocatVector4Pool(int poolCount)
        {
            int id = IUid++;
            PoolItem<Vector4> temp = new PoolItem<Vector4>(id, poolCount, typeof(Vector4));
            m_pools.Add(id,temp);
            return id;
        }
        public int AllocatLightProbePool(int poolCount)
        {
            int id = IUid++;
            PoolItem<SphericalHarmonicsL2> temp = new PoolItem<SphericalHarmonicsL2>(id, poolCount, typeof(SphericalHarmonicsL2));
            m_pools.Add(id,temp);
            return id;
        }
        #endregion

        #region AddData

        public void CopyData(int poolID,object source,int head,int length)
        {
            try
            {
                if (!m_pools.ContainsKey(poolID))
                {
                    return;
                }
                var pool = m_pools[poolID];
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

        public int GetPoolCount(int poolId)
        {
            if (poolId == -1)
            {
                return 0;
            }
            return m_pools[poolId].Count;
        }
        public List<Pool<Matrix4x4>> GetMatrix4X4s(int poolId)
        {
            return (List<Pool<Matrix4x4>>)GetData(poolId);
        }
        public List<Pool<float>> GetFloats(int poolId)
        {
            return (List<Pool<float>>)GetData(poolId);
        }
        public List<Pool<String>> GetString(int poolId)
        {
            return (List<Pool<String>>)GetData(poolId);
        }
        public List<Pool<Vector2>> GetVector2s(int poolId)
        {
            return (List<Pool<Vector2>>)GetData(poolId);
        }
        public List<Pool<Vector3>> GetVector3s(int poolId)
        {
            return (List<Pool<Vector3>>)GetData(poolId);
        }
        public List<Pool<Vector4>> GetVector4s(int poolId)
        {
            return (List<Pool<Vector4>>)GetData(poolId);
        }
        
        private object GetData(int poolId)
        {
            try
            {
                return m_pools[poolId].GetArray();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
        #endregion
        
        public void DisposePool(int id)
        {
            try
            {
                m_pools[id].Reset();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            
        }

        public void ReleasePool(int id)
        {
            if (id == -1)
            {
                return;
            }
            if (m_pools.ContainsKey(id))
            {
                m_pools[id].Destory();
                m_pools.Remove(id);
            }
        }
       
    }
}