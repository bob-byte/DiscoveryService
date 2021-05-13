using LUC.DiscoveryService.CodingData;
using System;
using System.IO;

namespace LUC.DiscoveryService.Messages
{
    public class MulticastMessage : Message
    {
        public MulticastMessage()
        {
            ;
        }

        /// <summary>
        /// Create a new instance of the <see cref="MulticastMessage"/> class.
        /// </summary>
        /// <param name="messageId">
        /// Unique message identifier. It is used to detect duplicate messages.
        /// </param>
        /// <param name="machineId">
        /// Id of machine which is sending this messege
        /// </param>
        /// <param name="tcpPort">
        /// TCP port which is being run in machine with <see cref="MachineId"/>
        /// </param>
        public MulticastMessage(UInt32 protocolVersion, UInt32 messageId, String machineId, UInt32 tcpPort)
            : base(messageId)
        {
            MachineId = machineId;
            TcpPort = tcpPort;
            ProtocolVersion = protocolVersion;
        }

        /// <summary>
        /// TCP port which is being run in machine with <see cref="MachineId"/>
        /// </summary>
        public UInt32 TcpPort { get; set; }

        /// <summary>
        /// Id of machine which is sending this messege
        /// </summary>
        public String MachineId { get; set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            else
            {
                MessageId = reader.ReadUInt32();
                ProtocolVersion = reader.ReadUInt32();
                MachineId = reader.ReadString();
                TcpPort = reader.ReadUInt32();

                return this;
            }
        }

        /// <inheritdoc/>
        public override void Write(WireWriter writer)
        {
            if(writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            else
            {
                writer.Write(MessageId);
                writer.Write(ProtocolVersion);
                writer.Write(MachineId);
                writer.Write(TcpPort);
            }
        }

        /// <inheritdoc/>
        public override String ToString()
        {
            using(var writer = new StringWriter())
            {
                writer.WriteLine("Multicast message");
                writer.WriteLine($"MessageId = {MessageId};\n" +
                                 $"MachineId = {MachineId};\n" +
                                 $"Tcp port = {TcpPort};\n" +
                                 $"Protocol version = {ProtocolVersion}");

                return writer.ToString();
            }
        }
    }
}
