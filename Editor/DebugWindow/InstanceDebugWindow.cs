using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Toggle = UnityEngine.UIElements.Toggle;
namespace Unity.MergeInstancingSystem.DebugWindow
{
    public class InstanceDebugWindow: EditorWindow
    {
        #region menu item
        [MenuItem("Window/Instance/DebugWindow", false, 100000)]
        static void ShowWindow()
        {
            var window = GetWindow<InstanceDebugWindow>("Instance Debug window");
            window.Show();
        }
        #endregion
        private ListView m_InstanceItemList;
        private List<InstanceItem> m_InstanceItems = new List<InstanceItem>();
        private List<InstanceItemData> m_InstanceItemDatas = new List<InstanceItemData>();
        private HierarchyItem m_selectedItem;

        private RadioButtonGroup m_drawModeUI;
        [SerializeField]
        private bool m_drawSelected = true;
        [SerializeField] 
        private bool m_highlightRendered = true;

        [SerializeField]
        private DrawMode m_drawMode = DrawMode.RenderOnly;
        
        public bool HighlightRendered => m_highlightRendered;
        
        private void OnEnable()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;
            
            MonoScript ms = MonoScript.FromScriptableObject(this);
            string scriptPath = AssetDatabase.GetAssetPath(ms);
            string scriptDirectory = Path.GetDirectoryName(scriptPath);
            
            // Import UXML
            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(scriptDirectory + "/InstanceDebugWindow.uxml");
            
            visualTree.CloneTree(root);
            
            //Initialize variables
            m_InstanceItemList = root.Q<ListView>("InstanceItemList");
            
            UpdateDataList();


            var serializedObject = new SerializedObject(this);
            var drawSelectedUI = root.Q<Toggle>("DrawSelected");
            drawSelectedUI.Bind(serializedObject);

            var highlightRenderedUI = root.Q<Toggle>("HighlightRendered");
            highlightRenderedUI.Bind(serializedObject);
            
            m_drawModeUI = root.Q<RadioButtonGroup>("DrawMode");
            m_drawModeUI.choices = new[]
            {
                DrawMode.None.ToString(),
                DrawMode.RenderOnly.ToString(),
                DrawMode.All.ToString(),
            };
            m_drawModeUI.Bind(serializedObject);
            
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            SceneView.duringSceneGui += SceneViewOnDuringSceneGui;
        }
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SceneView.duringSceneGui -= SceneViewOnDuringSceneGui;
        }
        
        private void UpdateDataList()
        {
            foreach (var InstanceItem in m_InstanceItems)
            {
                InstanceItem.Dispose();
            }
            
            m_InstanceItemDatas.Clear();
            m_InstanceItemList.Clear();
            m_InstanceItems.Clear();

            foreach (var controller in InstanceManager.Instance.ActiveControllers)
            {
                var data = new InstanceItemData();
                data.Initialize(controller);
                m_InstanceItemDatas.Add(data);
            }

            var view = m_InstanceItemList.hierarchy[0] as ScrollView;
            view.Clear();
            foreach (var data in m_InstanceItemDatas)
            {
                var item = new InstanceItem(this);
                item.BindData(data);
                view.Add(item);
                
                m_InstanceItems.Add(item);
                
            }
        }
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            UpdateDataList();
            m_selectedItem = null;
        }

        #region Debug rendering
        private void SceneViewOnDuringSceneGui(SceneView obj)
        {
            if (m_drawMode != DrawMode.None)
            {
                foreach (var itemData in m_InstanceItemDatas)
                {
                    itemData.Render(m_drawMode);
                }
            }
            if (m_drawSelected)
            {
                if ( m_selectedItem != null)
                {
                    InstanceTreeNodeRenderer.Instance.Render(m_selectedItem.Data.TreeNode, Color.red, 2.0f);
                }
            }
        }

        public void SetSelectItem(HierarchyItem item)
        {
            if ( m_selectedItem != null)
                m_selectedItem.UnselectItem();
            
            m_selectedItem = item;
            if ( m_selectedItem != null)
                m_selectedItem.SelectItem();
        }
        #endregion
    }
}