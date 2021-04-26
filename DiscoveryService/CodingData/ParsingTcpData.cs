using LUC.DiscoveryService.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace LUC.DiscoveryService.CodingData
{
    class ParsingTcpData : Parsing<TcpMessage>
    {
        public override Byte[] GetDecodedData(TcpMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            else
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(message.VersionOfProtocol);
                        writer.Write(message.GroupsSupported.Count);
                        foreach (var groupSupported in message.GroupsSupported)
                        {
                            writer.Write(((IPEndPoint)groupSupported.Key).Address.ToString());
                            writer.Write(((IPEndPoint)groupSupported.Key).Port);
                        }

                        var decodedData = stream.GetBuffer();

                        return decodedData;
                    }
                }
            }
        }

        public override TcpMessage GetEncodedData(Byte[] bytes)
        {
            if(bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            else
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var protocolVersion = reader.ReadInt32();
                        if (protocolVersion != Message.ProtocolVersion)
                        {
                            throw new ArgumentException("Bad version of protocol");
                        }

                        var countGroups = reader.ReadInt32();
                        var groupsSupported = new Dictionary<EndPoint, List<X509Certificate>>();
                        for (Int32 i = 0; i < countGroups; i++)
                        {
                            var address = IPAddress.Parse(reader.ReadString());
                            var port = reader.ReadInt32();

                            groupsSupported.Add(new IPEndPoint(address, port), new List<X509Certificate>());
                        }

                        var message = new TcpMessage(protocolVersion, groupsSupported);

                        return message;
                    }
                }
            }
        }
    }
}