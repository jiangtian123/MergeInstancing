using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.MergeInstancingSystem;

namespace Unity.MergeInstancingSystem
{
    public class InstanceSerializer
    {
        /// <summary>
        /// 将数据写入文件中
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="data"></param>
        public static void Write(Stream stream, InstanceData data)
        {
            //二进制格式序列化和反序列化对象
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, data);
        }

        public static InstanceData Read(Stream stream)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            return formatter.Deserialize(stream) as InstanceData;
        }
    }
}