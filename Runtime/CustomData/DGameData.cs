namespace Unity.MergeInstancingSystem
{
    /// <summary>
    /// 每个Gameobject用来做剔除操作时的替代
    /// </summary>
    public struct DElement
    {
        /// <summary>
        /// 标记该元素属于哪个类型
        /// </summary>
        public int m_mark;
        /// <summary>
        /// 该元素是否通过视锥剔除
        /// </summary>
        public bool m_visible;
        /// <summary>
        /// 选择哪个Lod级别
        /// </summary>
        public int m_lodLevel;
        /// <summary>
        /// 剔除使用
        /// </summary>
        public DAABB m_bounds;
        /// <summary>
        /// 判断Lod级别使用
        /// </summary>
        public DSphere m_sphers;
        
        /// <summary>
        /// 存放剔除矩阵据的Index
        /// </summary>
        public int m_dataIndex;

        /// <summary>
        /// 存放渲染矩阵的Index
        /// </summary>
        public int m_renderDataIndex;
        /// <summary>
        /// 存放光照数据的Inde
        /// </summary>
        public int m_lightDataIndex;
    }

    /// <summary>
    /// 因为一个
    /// </summary>
    public class DGameObjectData
    {
        public int m_originMatrixIndex;
        public int m_Lightindex;
    }
   
}