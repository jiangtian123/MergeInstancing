using System;

namespace Unity.MergeInstancingSystem.Pool
{
    public interface BasePool
    {
        public int ID { get; }

        public Type Type { get; }
        public int Capacity { get; }
        public int Count { get; }
        void Add(object item);

        void CopyToArray(object source, int head, int length);
        
        void Reset();

        object GetArray();
        
        void Destory();
        
    }
}