using System;
using UnityEngine;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public class RenderClassState
    {
        [SerializeField]
        public long m_identifier;
        [SerializeField]
        public int m_citations;
        [SerializeField]
        public bool m_useLightMap;

        public RenderClassState(long identifier,int m_citations,bool useLightMap)
        {
            this.m_identifier = identifier;
            this.m_citations = m_citations;
            this.m_useLightMap = useLightMap;
        }
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            return m_identifier == ((RenderClassState)obj).m_identifier;
        }
    }
}