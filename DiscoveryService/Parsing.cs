using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace DiscoveryServices
{
    class Parsing<T>
    {
        public static T GetEncodedData(Byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                T encodedData = (T)binaryFormatter.Deserialize(stream);

                return encodedData;
            }
        }

        public static Byte[] GetDecodedData(T obj)
        {
            using (var stream = new MemoryStream())
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(stream, obj);
                var decodedData = stream.GetBuffer();

                return decodedData;
            }
        }
    }
}
