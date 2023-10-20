using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.MergeInstancingSystem.Utils
{
    public static class MeshRendererExtension
    {
        public static WorkingObject ToWorkingObject(this MeshRenderer renderer, Allocator allocator)
        {
            WorkingObject obj = new WorkingObject(allocator);
            obj.FromRenderer(renderer);
            return obj;
        }
    }
    public class WorkingObject : IDisposable
    {
        private Allocator m_allocator;
        private Mesh m_mesh;
        private List<Material> m_materials;
        private LightProbeUsage m_lightProbeUsage;
        private Matrix4x4 m_localToWorld;
        public string Name { set; get; }
        public WorkingObject(Allocator allocator)
        {
            m_allocator = allocator;
            m_mesh = null;
            m_materials = new List<Material>();
            m_localToWorld = Matrix4x4.identity;
            m_lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
        }
        public void FromRenderer(MeshRenderer renderer)
        {
            //clean old data
            m_mesh?.Clear();
            m_materials?.Clear();
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            //CopyMesh数据
            if (filter != null && filter.sharedMesh != null)
            {
                m_mesh = filter.sharedMesh;
                foreach (var mat in renderer.sharedMaterials)
                {
                    m_materials.Add(mat);
                }

                m_localToWorld = renderer.localToWorldMatrix;

                m_lightProbeUsage = renderer.lightProbeUsage;
            }
        }

        public void Dispose()
        {
            
        }
    }
}