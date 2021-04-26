using LUC.DiscoveryService.Messages;
using System;
using System.IO;

namespace LUC.DiscoveryService.CodingData
{
    class ParsingMulticastData : Parsing<MulticastMessage>
    {
        public override MulticastMessage GetEncodedData(Byte[] bytes)
        {
            if(bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            else
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    MulticastMessage receivedMessage = null;
                    try
                    {
                        using (var reader = new BinaryReader(stream))
                        {
                            var protocolVersion = reader.ReadInt32();
                            if (protocolVersion != Message.ProtocolVersion)
                            {
                                throw new ArgumentException("Bad version of protocol");
                            }

                            var id = reader.ReadString();
                            var tcpPort = reader.ReadInt32();

                            receivedMessage = new MulticastMessage(id, tcpPort, protocolVersion);
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

        public override Byte[] GetDecodedData(MulticastMessage message)
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
