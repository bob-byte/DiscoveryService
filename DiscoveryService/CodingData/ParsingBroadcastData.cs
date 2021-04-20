using LUC.DiscoveryService.Messages;
using System;
using System.IO;

namespace LUC.DiscoveryService.CodingData
{
    class ParsingBroadcastData : Parsing<BroadcastMessage>
    {
        public override BroadcastMessage GetEncodedData(Byte[] bytes)
        {
            if(bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            else
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    BroadcastMessage receivedMessage = null;
                    try
                    {
                        using (var reader = new BinaryReader(stream))
                        {
                            var protocolVersion = reader.ReadInt32();
                            if (protocolVersion != Message.ProtocolVersion)
                            {
                                throw new Exception("Bad version of protocol");
                            }

                            var id = reader.ReadString();
                            var tcpPort = reader.ReadInt32();

                            receivedMessage = new BroadcastMessage(id, tcpPort, protocolVersion);
                        }
                    }
                    catch
                    {
                        throw;
                    }

                    return receivedMessage;
                }
            }
        }

        public override Byte[] GetDecodedData(BroadcastMessage message)
        {
            if(message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            else
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        Byte[] decodedData = null;
                        try
                        {
                            writer.Write(message.VersionOfProtocol);
                            writer.Write(message.MachineId);
                            writer.Write(message.TcpPort);

                            decodedData = stream.GetBuffer();
                        }
                        catch
                        {
                            throw;
                        }

                        return decodedData;
                    }
                }
            }
        }
    }
}
