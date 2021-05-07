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
                        using (var reader = new WireReader(stream))
                        {
                            var messageId = reader.ReadUInt32();
                            var protocolVersion = reader.ReadUInt32();
                            if (protocolVersion != Message.ProtocolVersion)
                            {
                                throw new ArgumentException("Bad version of protocol");
                            }
                            var machineId = reader.ReadString();
                            var tcpPort = reader.ReadUInt32();

                            receivedMessage = new MulticastMessage((Int32)messageId, machineId, (Int32)tcpPort, (Int32)protocolVersion);
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
            if(message != null)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (var writer = new WireWriter(stream))
                    {
                        Byte[] decodedData = null;
                        try
                        {
                            writer.Write((UInt32)message.MessageId);
                            writer.Write((UInt32)message.VersionOfProtocol);
                            writer.Write(message.MachineId);
                            writer.Write((UInt32)message.TcpPort);

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
            else
            {
                throw new ArgumentNullException(nameof(message));
            }
        }
    }
}
