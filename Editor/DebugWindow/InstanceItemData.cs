using System.Collections;
using System.Collections.Generic;
using Unity.MergeInstancingSystem.Controller;
using UnityEditor;
using UnityEngine;
namespace Unity.MergeInstancingSystem.DebugWindow
{
    public class InstanceItemData : ScriptableObject
    {
        private InstanceControllerBase m_controller;
        [SerializeField]
        private string m_name;
        private List<InstanceTreeNode> m_nodes = new List<InstanceTreeNode>();
        [SerializeField]
        private bool m_enableDebug = true;
        private List<HierarchyItemData> m_hierarchyItemDatas = new List<HierarchyItemData>();
        public InstanceControllerBase Controller
        {
            get
            {
                return m_controller;
            }
        }
        public List<HierarchyItemData> HierarchyItemDatas
        {
            get
            {
                return m_hierarchyItemDatas;
            }
        }
        public void Initialize(InstanceControllerBase controller)
        {
            Stack<InstanceTreeNode> treeNodeTravelStack = new Stack<InstanceTreeNode>();
            Stack<string> labelStack = new Stack<string>();

            m_controller = controller;
            m_name = controller.gameObject.name;
            
            treeNodeTravelStack.Push(controller.Root);
            labelStack.Push("");

            while (treeNodeTravelStack.Count > 0)
            {
                var node = treeNodeTravelStack.Pop();
                var label = labelStack.Pop();
                m_hierarchyItemDatas.Add(new HierarchyItemData()
                {
                    Index = m_hierarchyItemDatas.Count,
                    TreeNode = node,
                    Label = label,
                    IsOpen = true,
                });
                m_nodes.Add(node);
                
                for (int i = node.GetChildTreeNodeCount() - 1; i >= 0; --i)
                {
                    treeNodeTravelStack.Push(node.GetChildTreeNode(i));
                    labelStack.Push($"{label}_{i+1}");
                }
            }
        }
        public void CleanUp()
        {
            m_controller = null;
            m_nodes.Clear();
            m_hierarchyItemDatas.Clear();
        }
        public void Render(DrawMode drawMode)
        {
            if (m_enableDebug == false)
                return;
            
            foreach (var node in m_nodes)
            {
                if (node.ExprectedState == InstanceTreeNode.State.Low)
                {
                    InstanceTreeNodeRenderer.Instance.Render(node, Color.magenta, 2.0f);
                }

                if (node.ExprectedState == InstanceTreeNode.State.High)
                {
                    InstanceTreeNodeRenderer.Instance.Render(node, Color.blue, 2.0f);
                }
                else if (drawMode == DrawMode.All)
                {
                    InstanceTreeNodeRenderer.Instance.Render(node, Color.yellow, 1.0f);
                }
            }
        }
    }
}