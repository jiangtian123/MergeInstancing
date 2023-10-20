using System;
using System.Linq;
using Unity.MergeInstancingSystem.InstanceBuild;
using Unity.MergeInstancingSystem.MeshUtils;
using Unity.MergeInstancingSystem.SpaceManager;
using Unity.MergeInstancingSystem.Utils;
using Unity.MergeInstancingSystem;
using UnityEditor;
using UnityEngine;
namespace Unity.MergeInstancingSystem
{
    [CustomEditor(typeof(Instance))]
    public class InstanceEditor: Editor
    {
        public static class Styles
        {
            public static GUIContent GenerateButtonEnable = new GUIContent("Generate", "Generate Instance mesh.");
            public static GUIContent GenerateButtonExists = new GUIContent("Generate", "Instance already generated.");
            public static GUIContent DestroyButtonEnable = new GUIContent("Destroy", "Destroy Instance mesh.");
            public static GUIContent DestroyButtonNotExists = new GUIContent("Destroy", "Instance must be created before the destroying.");

            public static GUIStyle RedTextColor = new GUIStyle();
            public static GUIStyle BlueTextColor = new GUIStyle();

            static Styles()
            {
                RedTextColor.normal.textColor = Color.red;
                BlueTextColor.normal.textColor = new Color(0.4f, 0.5f, 1.0f);
            }

        }
        
        private SerializedProperty m_ChunkSizeProperty;
        private SerializedProperty m_LODDistanceProperty;
        private SerializedProperty m_CullDistanceProperty;
        private SerializedProperty m_MinObjectSizeProperty;
        private SerializedProperty m_useMotionvectoruseMotionvector;
        private LODSlider m_LODSlider;
        
        private Type[] m_SpaceSplitterTypes;
        private string[] m_SpaceSplitterNames;

        private Type[] m_MeshUtilsType;
        private string[] m_MeshUtilsNames;

        private Type[] m_InstanceBuildType;
        private string[] m_InstanceBuildNames;
        
        private bool isShowCommon = true;
        private bool isShowSpaceSplitter = true;
        private bool isShowMeshUtils = true;
        private bool isShowInstanceBuild = true;
        
        private bool isFirstOnGUI = true;
        
        private ISpaceSplitter m_splitter;
        
        [InitializeOnLoadMethod]
        static void InitTagTagUtils()
        {
            if (LayerMask.NameToLayer(Instance.InstanceLayerStr) == -1)
            {
                TagUtils.AddLayer(Instance.InstanceLayerStr);
                Tools.lockedLayers |= 1 << LayerMask.NameToLayer(Instance.InstanceLayerStr);
            }
        }
        
        private void OnEnable()
        {
            //serializedObject.FindProperty 从目标脚本的序列化属性中找到这个属性。
            m_ChunkSizeProperty = serializedObject.FindProperty("m_ChunkSize");
            m_LODDistanceProperty = serializedObject.FindProperty("m_LODDistance");
            m_CullDistanceProperty = serializedObject.FindProperty("m_CullDistance");
            m_MinObjectSizeProperty = serializedObject.FindProperty("m_MinObjectSize");
            m_useMotionvectoruseMotionvector = serializedObject.FindProperty("m_UseMotionvector");
            //创建一个滑条
            m_LODSlider = new LODSlider(true, "Cull");
            m_LODSlider.InsertRange("High", m_LODDistanceProperty);
            m_LODSlider.InsertRange("Low", m_CullDistanceProperty);
            
            m_SpaceSplitterTypes = SpaceSplitterTypes.GetTypes();
            m_SpaceSplitterNames = m_SpaceSplitterTypes.Select(t => t.Name).ToArray();

            m_MeshUtilsType = MeshUtilsType.GetTypes();
            m_MeshUtilsNames = m_MeshUtilsType.Select(t => t.Name).ToArray();

            m_InstanceBuildType = InstanceBuilderTypes.GetTypes();
            m_InstanceBuildNames = m_InstanceBuildType.Select(t => t.Name).ToArray();
            
            isFirstOnGUI = true;
        }
        public static float GetChunkSizePropertyValue(float value)
        {
            if (value < 0.05f)
            {
                return 0.05f;
            }
            return value;
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            
            Instance instance = target as Instance;
            if (instance == null)
            {
                EditorGUILayout.LabelField("Instance is null.");
                return;
            }
            if (m_splitter == null)
            {
                m_splitter = SpaceSplitterTypes.CreateInstance(instance);
            }
            isShowCommon = EditorGUILayout.BeginFoldoutHeaderGroup(isShowCommon, "Common");
            if (isShowCommon == true)
            {
                EditorGUILayout.PropertyField(m_ChunkSizeProperty);
                EditorGUILayout.PropertyField(m_useMotionvectoruseMotionvector);
                m_ChunkSizeProperty.floatValue = GetChunkSizePropertyValue(m_ChunkSizeProperty.floatValue);
                
                if (m_splitter != null)
                {
                    var bounds = instance.GetBounds();
                    int depth = m_splitter.CalculateTreeDepth(bounds, m_ChunkSizeProperty.floatValue);
                    
                    EditorGUILayout.LabelField($"The Instance tree will be created with {depth} levels.", Styles.BlueTextColor);
                    if (depth > 5)
                    {
                        EditorGUILayout.LabelField($"Node Level Count greater than 5 may cause a frozen Editor.",
                            Styles.RedTextColor);
                        EditorGUILayout.LabelField($"I recommend keeping the level under 5.", Styles.RedTextColor);
                    
                    }
                }
                m_LODSlider.Draw();
                EditorGUILayout.PropertyField(m_MinObjectSizeProperty);
            }
            //结束创建折叠头组
            EditorGUILayout.EndFoldoutHeaderGroup();
            isShowSpaceSplitter = EditorGUILayout.BeginFoldoutHeaderGroup(isShowSpaceSplitter, "SpaceSplitter");
            if (isShowSpaceSplitter)
            {
                EditorGUI.indentLevel += 1;
                if (m_SpaceSplitterTypes.Length > 0)
                {
                    EditorGUI.BeginChangeCheck();
                    
                    int spaceSplitterIndex = Math.Max(Array.IndexOf(m_SpaceSplitterTypes, instance.SpaceSplitterType), 0);
                    spaceSplitterIndex = EditorGUILayout.Popup("SpaceSplitter", spaceSplitterIndex, m_SpaceSplitterNames);
                    instance.SpaceSplitterType = m_SpaceSplitterTypes[spaceSplitterIndex];

                    var info = m_SpaceSplitterTypes[spaceSplitterIndex].GetMethod("OnGUI");
                    if (info != null)
                    {
                        if ( info.IsStatic == true )
                        {
                            info.Invoke(null, new object[] { instance.SpaceSplitterOptions });
                        }
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        m_splitter = SpaceSplitterTypes.CreateInstance(instance);
                    }

                    if (m_splitter != null)
                    {
                        int subTreeCount = m_splitter.CalculateSubTreeCount(instance.GetBounds());
                        EditorGUILayout.LabelField($"The instance tree will be created with {subTreeCount} sub trees.",
                            Styles.BlueTextColor);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Cannot find SpaceSplitters.");
                }
                EditorGUI.indentLevel -= 1;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            isShowMeshUtils =  EditorGUILayout.BeginFoldoutHeaderGroup(isShowMeshUtils, "MeshBatch");
            if (isShowMeshUtils)
            {
                
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            isShowInstanceBuild = EditorGUILayout.BeginFoldoutHeaderGroup(isShowInstanceBuild, "InstanceBuild");
            if (isShowInstanceBuild)
            {
                EditorGUI.indentLevel += 1;
                int BuildingIndex = Math.Max(Array.IndexOf(m_InstanceBuildType, instance.BuildType), 0);
                BuildingIndex = EditorGUILayout.Popup("Build", BuildingIndex, m_InstanceBuildNames);
                instance.BuildType = m_InstanceBuildType[BuildingIndex];
                
                var info = m_InstanceBuildType[BuildingIndex].GetMethod("OnGUI");
                
                if (info != null)
                {
                    if (info.IsStatic == true)
                    {
                        info.Invoke(null, new object[] { instance.BuildOptions });
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Cannot find BuildSetters.");
                }
                EditorGUI.indentLevel -= 1;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            GUIContent generateButton = Styles.GenerateButtonEnable;
            GUIContent destroyButton = Styles.DestroyButtonNotExists;
            
            if (instance.GeneratedObjects.Count > 0 )
            {
                generateButton = Styles.GenerateButtonExists;
                destroyButton = Styles.DestroyButtonEnable;
            }
            
            EditorGUILayout.Space();
            
            GUI.enabled = generateButton == Styles.GenerateButtonEnable;
            if (GUILayout.Button(generateButton))
            {
                CoroutineRunner.RunCoroutine(InstanceCreate.Create(instance));
            }
            GUI.enabled = destroyButton == Styles.DestroyButtonEnable;
            if (GUILayout.Button(destroyButton))
            {
                CoroutineRunner.RunCoroutine(InstanceCreate.Destroy(instance));
            }
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(instance);
            }

            GUI.enabled = true;

            
            serializedObject.ApplyModifiedProperties();
            isFirstOnGUI = false;
        }
    }
}