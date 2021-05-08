using LUC.DiscoveryService.Messages;
using System;
using System.IO;
using System.Text;

namespace LUC.DiscoveryService.CodingData
{
    class ParsingTcpData : Parsing<TcpMessage>
    {
        private readonly SupportedTcpCodingTypes supportedTypes = new SupportedTcpCodingTypes();

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
                    using (var writer = new WireWriter(stream))
                    {
                        try
                        {
                            writer.WriteByte(supportedTypes[PropertyInTcpMessage.MessageId]);
                            writer.Write(message.MessageId);

                            writer.WriteByte(supportedTypes[PropertyInTcpMessage.VersionOfProtocol]);
                            writer.Write(message.VersionOfProtocol);

                            writer.WriteByte(supportedTypes[PropertyInTcpMessage.GroupsSupported]);
                            var namesOfGroupsInGroupsSupported = message.GroupsSupported.Keys;
                            writer.WriteEnumerable(namesOfGroupsInGroupsSupported);
                            var certificates = message.GroupsSupported.Values;
                            writer.WriteEnumerable(certificates);

                            writer.WriteByte(supportedTypes[PropertyInTcpMessage.KnownIps]);
                            var namesOfGroupsInKnownIps = message.KnownIps.Keys;
                            writer.WriteEnumerable(namesOfGroupsInKnownIps);
                            var ipNetworks = message.KnownIps.Values;
                            writer.WriteEnumerable(ipNetworks);

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
                        catch(InvalidDataException)
                        {
                            throw;
                        }
                    }
                }
            }
        }
        private Object locker = new Object();
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
                    using (var reader = new WireReader(stream))
                    {
                        TcpMessage message = new TcpMessage();
                        try
                        {
                            lock (locker)
                            {
                                for (var property = PropertyInTcpMessage.First; property <= PropertyInTcpMessage.Last; property++)
                                {

                                    var typeAsByte = reader.ReadByte();

                                    switch (supportedTypes[typeAsByte])
                                    {
                                        case PropertyInTcpMessage.MessageId:
                                            {
                                                message.MessageId = reader.ReadUInt32();
                                                break;
                                            }
                                        case PropertyInTcpMessage.VersionOfProtocol:
                                            {
                                                message.VersionOfProtocol = reader.ReadUInt32();
                                                if (message.VersionOfProtocol != Message.ProtocolVersion)
                                                {
                                                    throw new ArgumentException("Bad version of protocol");
                                                }

                                                break;
                                            }
                                        case PropertyInTcpMessage.GroupsSupported:
                                            {
                                                message.GroupsSupported = reader.DictionaryFromMessage();
                                                break;
                                            }
                                        case PropertyInTcpMessage.KnownIps:
                                            {
                                                message.KnownIps = reader.DictionaryFromMessage();
                                                break;
                                            }
                                    }
                                }

                                return message;
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
}
