using Unity.MergeInstancingSystem;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
namespace Unity.MergeInstancingSystem
{
    public class InstanceBuilder: IProcessSceneWithReport
    {
         public int callbackOrder
        {
            get { return 0; }
        }
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();


            for (int oi = 0; oi < rootObjects.Length; ++oi)
            {
                List<Instance> hlods = new List<Instance>();

                FindComponentsInChild(rootObjects[oi], ref hlods);
                for (int hi = 0; hi < hlods.Count; ++hi)
                {
                    Object.DestroyImmediate(hlods[hi]);
                }
            }
        }

        private void FindComponentsInChild<T>(GameObject target, ref List<T> components)
        {
            var component = target.GetComponent<T>();
            if (component != null)
                components.Add(component);

            foreach (Transform child in target.transform)
            {
                FindComponentsInChild(child.gameObject, ref components);
            }
        }
    }
}