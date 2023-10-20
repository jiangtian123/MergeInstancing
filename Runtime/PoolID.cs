namespace Unity.MergeInstancingSystem
{
    public class PoolID
    {
        public int m_matrix4x4ID ;
        public int m_lightMapIndexId;
        public int m_lightMapScaleOffsetID;
        public PoolID()
        {
            m_matrix4x4ID = -1;
            m_lightMapIndexId = -1;
            m_lightMapScaleOffsetID = -1;
        }
    }
}