using DiscoveryServices.Messages;
using System;
using System.IO;

namespace DiscoveryServices.CodingData
{
    class ParsingBroadcastData : Parsing<BroadcastMessage>
    {
        public override BroadcastMessage GetEncodedData(Byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var id = reader.ReadString();
                    var tcpPort = reader.ReadInt32();
                    var protocolVersion = reader.ReadInt32();

                    var message = new BroadcastMessage(id, tcpPort, protocolVersion);

                    return message;
                }
            }
        }

        public override Byte[] GetDecodedData(BroadcastMessage message)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(message.Id);
                    writer.Write(message.TcpPort);
                    writer.Write(message.VersionOfProtocol);

                    var decodedData = stream.GetBuffer();

                    return decodedData;
                }
            }
        }
    }
}
