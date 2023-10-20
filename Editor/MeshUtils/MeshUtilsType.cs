using System;
using System.Collections.Generic;
using System.Linq;
namespace Unity.MergeInstancingSystem.MeshUtils
{
    public class MeshUtilsType
    {
        struct OrderType
        {
            public int Order;
            public Type type;
        }
        private static List<OrderType> s_Types = new List<OrderType>();

        public static void RegisterType(Type type, int order = 0)
        {
            s_Types.Add(new OrderType()
            {
                Order = order,
                type = type,
            });

            s_Types.Sort((lhs, rhs) => lhs.Order - rhs.Order);
        }

        public static void UnregisterType(Type type)
        {
            for (int i = 0; i < s_Types.Count; ++i)
            {
                if (s_Types[i].type == type)
                {
                    s_Types.RemoveAt(i);
                    return;
                }
            }
        }

        public static Type[] GetTypes()
        {
            return s_Types.Select(t=>t.type).ToArray();
        }
    }
}