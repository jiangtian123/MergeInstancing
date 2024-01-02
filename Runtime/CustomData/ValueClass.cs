using System;
using System.Collections.Generic;

namespace Unity.MergeInstancingSystem
{
    [Serializable]
    public struct NodeGameObject
    {
        public int head;
        public int number;
    }

    public struct JobTreeData
    {
        public DAABB m_box;
        public int objhead;
        public int objlength;
        public int child_0;
        public int child_1;
        public int child_2;
        public int child_3;
        public bool hasChild;
        public JobTreeData(DAABB box, List<int> child,int head,int length)
        {
            m_box = box;
            if (child.Count == 0)
            {
                hasChild = false;
                child_0 = -1;
                child_1 = -1;
                child_2 = -1;
                child_3 = -1;
            }
            else
            {
                hasChild = true;
                child_0 = child[0];
                child_1 = child[1];
                child_2 = child[2];
                child_3 = child[3];
            }

            objhead = head;
            objlength = length;
        }
    }
}