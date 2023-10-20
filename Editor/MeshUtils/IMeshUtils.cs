using System;

namespace Unity.MergeInstancingSystem.MeshUtils
{
    public interface IMeshUtils :  IDisposable
    {
        void PeocessInstanceData(Action<float> onProgress);
    }
}