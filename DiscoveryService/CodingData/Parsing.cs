using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace LUC.DiscoveryService.CodingData
{
    class Parsing<T>
    {
        /// <summary>
        /// It use BinaryFormatter, so too many spaces and time
        /// </summary>
        public virtual T GetEncodedData(Byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                T encodedData = (T)binaryFormatter.Deserialize(stream);

                return encodedData;
            }
        }

        /// <summary>
        /// It use BinaryFormatter, so too many spaces and time
        /// </summary>
        public virtual Byte[] GetDecodedData(T obj)
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
