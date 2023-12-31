﻿using System.Collections.Generic;
using UnityEngine;
namespace Unity.MergeInstancingSystem
{
    public interface IGeneratedResourceManager
    {
        void AddGeneratedResource(Object obj);
        bool IsGeneratedResource(Object obj);

        void AddConvertedPrefabResource(GameObject obj);
    }
}