using LUC.DiscoveryService.Messages;
using System;
using System.IO;
using System.Text;

namespace LUC.DiscoveryService.CodingData
{
    class ParsingUdpData : Parsing<MulticastMessage>
    {
        private readonly SupportedUdpCodingTypes supportedTypes = new SupportedUdpCodingTypes();

        public override Byte[] GetDecodedData(MulticastMessage message)
        {
            if(message != null)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (var writer = new WireWriter(stream))
                    {
                        try
                        {
                            writer.WriteByte(supportedTypes[PropertyInUdpMessage.MessageId]);
                            writer.Write(message.MessageId);

                            writer.WriteByte(supportedTypes[PropertyInUdpMessage.VersionOfProtocol]);
                            writer.Write(message.VersionOfProtocol);

                            writer.WriteByte(supportedTypes[PropertyInUdpMessage.MachineId]);
                            writer.Write(message.MachineId);

                            writer.WriteByte(supportedTypes[PropertyInUdpMessage.TcpPort]);
                            writer.Write(message.TcpPort);

                            return stream.GetBuffer();
                        }
                        catch (EncoderFallbackException)
                        {
                            throw;
                        }
                        catch (ArgumentException)
                        {
                            throw;
                        }
                        catch (InvalidDataException)
                        {
                            throw;
                        }
                    }
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(message));
            }
        }

        public override MulticastMessage GetEncodedData(Byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            else
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    try
                    {
                        using (var reader = new WireReader(stream))
                        {
                            MulticastMessage receivedMessage = new MulticastMessage();

                            for (var property = PropertyInUdpMessage.First; property <= PropertyInUdpMessage.Last; property++)
                            {
                                var typeAsByte = reader.ReadByte();

                                switch(supportedTypes[typeAsByte])
                                {
                                    case PropertyInUdpMessage.MessageId:
                                        {
                                            receivedMessage.MessageId = reader.ReadUInt32();
                                            break;
                                        }
                                    case PropertyInUdpMessage.MachineId:
                                        {
                                            receivedMessage.MachineId = reader.ReadString();
                                            break;
                                        }
                                    case PropertyInUdpMessage.VersionOfProtocol:
                                        {
                                            receivedMessage.VersionOfProtocol = reader.ReadUInt32();
                                            if (receivedMessage.VersionOfProtocol != Message.ProtocolVersion)
                                            {
                                                throw new ArgumentException("Bad version of protocol");
                                            }

                                            break;
                                        }
                                    case PropertyInUdpMessage.TcpPort:
                                        {
                                            receivedMessage.TcpPort = reader.ReadUInt32();
                                            break;
                                        }
                                }
                            }

                            return receivedMessage;
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        throw;
                    }
                    catch (IOException)
                    {
                        throw;
                    }

                }
            }
        }
    }
}
