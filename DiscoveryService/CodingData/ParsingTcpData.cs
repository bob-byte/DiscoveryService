using LUC.DiscoveryService.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

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
                            writer.Write(groupSupported.Key);
                            writer.Write(groupSupported.Value.Count);
                            foreach (var nameOfGroups in groupSupported.Value)
                            {
                                writer.Write(nameOfGroups);
                            }
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
                        try
                        {
                            var protocolVersion = reader.ReadInt32();
                            if (protocolVersion != Message.ProtocolVersion)
                            {
                                throw new ArgumentException("Bad version of protocol");
                            }

                            var countPeers = reader.ReadInt32();
                            var groupsOfEachPeer = new ConcurrentDictionary<String, List<String>>();
                            for (Int32 i = 0; i < countPeers; i++)
                            {
                                var iPEndPoint = reader.ReadString();
                                var countGroupOfCurrentPeer = reader.ReadInt32();

                                List<String> groups = new List<String>(countGroupOfCurrentPeer);
                                for (Int32 nameGroup = 0; nameGroup < countGroupOfCurrentPeer; nameGroup++)
                                {
                                    groups.Add(reader.ReadString());
                                }
                                groupsOfEachPeer.TryAdd(iPEndPoint, groups);
                            }

                            return new TcpMessage(protocolVersion, groupsOfEachPeer);
                        }
                        catch(IOException)
                        {
                            throw;
                        }
                    }
                }
            }
        }
    }
}