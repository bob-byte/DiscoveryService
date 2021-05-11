using LUC.DiscoveryService.CodingData;
using System;
using System.IO;
using System.Text;

namespace LUC.DiscoveryService.Messages
{
    public class MulticastMessage : Message
    {
        public MulticastMessage()
        {
            DoNothing();
        }

        public MulticastMessage(UInt32 messageId, String machineId, UInt32 tcpPort)
            : base(messageId)
        {
            MachineId = machineId;
            TcpPort = tcpPort;
            VersionOfProtocol = ProtocolVersion;
        }

        public MulticastMessage(UInt32 messageId, String machineId, UInt32 tcpPort, UInt32 receivedProtocolVersion)
            : base(messageId)
        {
            MachineId = machineId;
            TcpPort = tcpPort;
            VersionOfProtocol = receivedProtocolVersion;
        }

        public UInt32 TcpPort { get; set; }

        public String MachineId { get; set; }

        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            else
            {
                try
                {
                    MessageId = reader.ReadUInt32();
                    VersionOfProtocol = reader.ReadUInt32();
                    MachineId = reader.ReadString();
                    TcpPort = reader.ReadUInt32();
                }
                catch (EndOfStreamException)
                {
                    throw;
                }
                catch (IOException)
                {
                    throw;
                }

                return this;
            }
        }

        public override void Write(WireWriter writer)
        {
            if(writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            else
            {
                try
                {
                    writer.Write(MessageId);
                    writer.Write(VersionOfProtocol);
                    writer.Write(MachineId);
                    writer.Write(TcpPort);
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

        public override String ToString()
        {
            using(var writer = new StringWriter())
            {
                writer.WriteLine("Multicast message\n");
                writer.Write($"MessageId = {MessageId};\n" +
                             $"MachineId = {MachineId};\n" +
                             $"Tcp port = {TcpPort};\n" +
                             $"Protocol version = {VersionOfProtocol}\n");

                return writer.ToString();
            }
        }
    }
}
