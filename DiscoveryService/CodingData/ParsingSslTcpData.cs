using DiscoveryServices.Messages;
using System;
using System.Collections.Generic;
using System.IO;

namespace DiscoveryServices.CodingData
{
    class ParsingSslTcpData : Parsing<SslTcpMessage>
    {
        public override Byte[] GetDecodedData(SslTcpMessage message)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(message.VersionOfProtocol);
                    writer.Write(message.GroupsSupported.Count);
                    foreach (var groupSupported in message.GroupsSupported)
                    {
                        writer.Write(groupSupported);
                    }

                    var decodedData = stream.GetBuffer();

                    return decodedData;
                }
            }
        }

        public override SslTcpMessage GetEncodedData(Byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var protocolVersion = reader.ReadInt32();
                    var countGroups = reader.ReadInt32();
                    var groupsSupported = new List<String>();
                    for (Int32 i = 0; i < countGroups; i++)
                    {
                        groupsSupported.Add(reader.ReadString());
                    }

                    var message = new SslTcpMessage(protocolVersion, groupsSupported);

                    return message;
                }
            }
        }
    }
}
