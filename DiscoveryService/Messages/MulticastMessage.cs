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
            ;
        }

        /// <summary>
        ///   Create a new instance of the <see cref="MulticastMessage"/> class.
        /// </summary>
        public MulticastMessage(UInt32 messageId, String machineId, UInt32 tcpPort)
            : base(messageId)
        {
            MachineId = machineId;
            TcpPort = tcpPort;
            VersionOfProtocol = ProtocolVersion;
        }

        /// <summary>
        ///   Create a new instance of the <see cref="MulticastMessage"/> class.
        /// </summary>
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
                    writer.Write(MessageId);
                    writer.Write(VersionOfProtocol);
                    writer.Write(MachineId);
                    writer.Write(TcpPort);
            }
        }

        public override String ToString()
        {
            using(var writer = new StringWriter())
            {
                writer.WriteLine("Multicast message");
                writer.WriteLine($"MessageId = {MessageId};\n" +
                             $"MachineId = {MachineId};\n" +
                             $"Tcp port = {TcpPort};\n" +
                             $"Protocol version = {VersionOfProtocol}");

                return writer.ToString();
            }
        }
    }
}
