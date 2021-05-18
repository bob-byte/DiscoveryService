using LUC.DiscoveryService.CodingData;
using System;
using System.IO;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    /// Allows to write and read multicast message to/from <see cref="Stream"/>
    /// </summary>
    public class MulticastMessage : Message
    {
        /// <summary>
        /// Create a new instance of the <see cref="MulticastMessage"/> class. This constructor is often used to read message
        /// </summary>
        public MulticastMessage()
        {
            ;
        }

        /// <summary>
        /// TCP port of Discovery Service
        /// </summary>
        public UInt32 TcpPort { get; set; }

        /// <summary>
        /// Create a new instance of the <see cref="MulticastMessage"/> class. This constructor is often used to write message to a stream
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
        public MulticastMessage(UInt32 messageId, UInt32 tcpPort, String machineId)
            : base(messageId)
        {
            TcpPort = tcpPort;
            MachineId = machineId;
        }

        /// <summary>
        /// Id of machine which is sending this messege
        /// </summary>
        public String MachineId { get; set; }

        /// <summary>
        /// TCP port which is being run in machine with machineId
        /// </summary>
        public UInt32 TcpPort { get; set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                MessageId = reader.ReadUInt32();
                VersionOfProtocol = reader.ReadUInt32();
                TcpPort = reader.ReadUInt32();
                MachineId = reader.ReadString();

                return this;
            }
            else
            {
                throw new ArgumentNullException(nameof(reader));
            }
        }

        /// <inheritdoc/>
        public override void Write(WireWriter writer)
        {
            if(writer != null)
            {
                writer.Write(MessageId);
                writer.Write(VersionOfProtocol);
                writer.Write(TcpPort);
                writer.Write(MachineId);
            }
            else
            {
                throw new ArgumentNullException(nameof(writer));
            }
        }

        /// <inheritdoc/>
        public override String ToString()
        {
            using(var writer = new StringWriter())
            {
                writer.WriteLine("Multicast message:");
                writer.WriteLine($"{base.ToString()};");
                writer.WriteLine($"TCP port = {TcpPort};\n" +
                                 $"MachineId = {MachineId}");

                return writer.ToString();
            }
        }
    }
}
