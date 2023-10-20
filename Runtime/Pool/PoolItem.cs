using System;
using System.Collections.Generic;
using UnityEngine;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace Unity.MergeInstancingSystem.Pool
{
    public class Pool<T>
    {
        public T[] OnePool;
        public int length;
        public int capacity;
        public bool IsFull
        {
            get
            {
                return length == capacity;
            }
        }

        public int Tail
        {
            get
            {
                return capacity - length;
            }
        }
        public Pool(int capacity)
        {
            OnePool = new T[capacity];
            this.capacity = capacity;
            length = 0;
        }
    }
    /// <summary>
    /// 申请一种类型的常驻内存的连续数组
    /// </summary>
    public class PoolItem<T> :BasePool
    {
        /// <summary>
        /// 一个buffer最大的容积就是1000
        /// </summary>
        private readonly int MAX_BUFFCOUNT = 1000;

        private readonly int EXPANDE_NUMBER = 2;
        /// <summary>
        /// 渲染数据存放的地方，一个pool是一千的容量
        /// </summary>
        
        private List<Pool<T>> m_item;
        
        private Type m_type;
        /// <summary>
        /// 当前向哪个Pool里添加
        /// </summary>
        private int m_Index;

        private int m_id;
        public int Count
        {
            get
            {
                return m_Index +1 ;
            }
        }
        
        public int ID
        {
            get
            {
                return m_id;
            }
        }
        
        public Type Type
        {
            get
            {
                return m_type;
            }
        }
        /// <summary>
        /// 当前有几个Pool，初始化时，每超过1000就会申请一个Pool
        /// </summary>
        public int Capacity
        {
            get
            {
                return m_item.Count;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="itemSize"></param>
        /// <param name="cout"></param>
        /// <param name="type"></param>
        public PoolItem(int id,int count ,Type type)
        {
            m_id = id;
            m_type = type;
            m_Index = 0;
            // --------------------- 还是得按照一个pool的容量不能超过1000 来做 --------------------------------- 
            int size =  Mathf.CeilToInt((float)count / MAX_BUFFCOUNT);
            
            m_item = new List<Pool<T>>();
            for (int i = 0; i < size; i++)
            {
                if (i == size-1)
                {
                    Pool<T> temp = new Pool<T>(count % MAX_BUFFCOUNT);
                    m_item.Add(temp);
                }
                else
                {
                    Pool<T> temp = new Pool<T>(MAX_BUFFCOUNT);
                    m_item.Add(temp);
                }
            }
        }

        public object GetArray()
        {
            return m_item;
        }
        /// <summary>
        /// 加一个元素
        /// </summary>
        /// <param name="item"></param>
        public void Add(object item)
        {
            //当前list中的使用的那个pool满了
            if (m_item[m_Index].IsFull)
            {
                Expande();
                m_Index += 1;
            }

            var pool = m_item[m_Index];
            pool.OnePool[pool.length] = (T)item;
            pool.length += 1;
        }
        /// <summary>
        /// 拷贝多数元素
        /// </summary>
        /// <param name="source">原数组</param>
        /// <param name="head">原数组的开头</param>
        /// <param name="length">要拷贝的长度</param>
        
        public void CopyToArray(object source,int head,int length)
        {
            var so = (T[])source;
            //
            int paramHead = 0;
            var meshCount = length;
            while (paramHead < meshCount)
            {
                var pool = m_item[m_Index];
                //当前使用的Pool已经满了就扩张一张
                if (pool.IsFull)
                {
                    m_Index++;
                    if (m_Index == m_item.Count)
                    {
                        Debug.Log("在扩张内存，绝对有问题!!!!!!!!");
                        Expande();
                    }
                    pool = m_item[m_Index];
                }
                //如果拷贝的数量小于pool的长度，取拷贝数量，不然取容器长度
                var copyLen = Math.Min(meshCount - paramHead, pool.Tail);
                Array.Copy(so, head+paramHead, pool.OnePool, pool.length, copyLen);
                paramHead += copyLen;
                pool.length += copyLen;
            }
        }
        public void Reset()
        {
            foreach (var pool in m_item)
            {
                pool.length = 0;
            }
            m_Index = 0;
        }
        /// <summary>
        /// 如果塞满了，再加两个
        /// </summary>
        public void Expande()
        {
            for (int i = 0; i < EXPANDE_NUMBER; i++)
            {
                Pool<T> temp = new Pool<T>(MAX_BUFFCOUNT);
                m_item.Add(temp);
            }
        }
        
        public void Destory()
        {
            foreach (var pool in m_item)
            {
                pool.OnePool = null;
            }
            m_item.Clear();
        }
    }
}