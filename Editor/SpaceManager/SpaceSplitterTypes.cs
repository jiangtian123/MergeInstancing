using System;
using System.Collections.Generic;
namespace Unity.MergeInstancingSystem.SpaceManager
{
    public static class SpaceSplitterTypes
    {

        private static List<Type> s_Types = new List<Type>();

        public static void RegisterSpaceSplitterType(Type type)
        {
            s_Types.Add(type);
        }

        public static void UnregisterSpaceSplitterType(Type type)
        {
            s_Types.Remove(type);
        }

        public static Type[] GetTypes()
        {
            return s_Types.ToArray();
        }

        /// <summary>
        /// 返回在Editor下设置的空间分割的类型
        /// </summary>
        /// <param name="hlod"></param>
        /// <returns></returns>
        public static ISpaceSplitter CreateInstance(Instance instance)
        {
            //判断该类型有没有。没有返回null
            if (s_Types.IndexOf(instance.SpaceSplitterType) < 0)
                return null;
            //CreateInstance 接受两个参数，一个类，一个是类的构造函数的参数
            ISpaceSplitter spaceSplitter =
                (ISpaceSplitter)Activator.CreateInstance(instance.SpaceSplitterType,
                    new object[] { instance.SpaceSplitterOptions });

            return spaceSplitter;
        }

    }
}