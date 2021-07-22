using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using System;
using System.IO;
using System.Numerics;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    /// Allows to write and read multicast message to/from <see cref="Stream"/>
    /// </summary>
    public class UdpMessage : DiscoveryServiceMessage
    {
        /// <summary>
        /// Maximum bytes of a message.
        /// </summary>
        /// <remarks>
        /// In reality the max length is dictated by the network MTU.
        /// </remarks>
        public const Int32 MaxLength = 10240;

        /// <summary>
        /// Create a new instance of the <see cref="UdpMessage"/> class. This constructor is often used to read message
        /// </summary>
        public UdpMessage()
        {
            ;//do nothing
        }

        /// <summary>
        /// Create a new instance of the <see cref="UdpMessage"/> class. This constructor is often used to write message to a stream
        /// </summary>
        /// <param name="messageId">
        /// Unique message identifier. It is used to detect duplicate messages.
        /// </param>
        /// <param name="tcpPort">
        /// TCP port which is being run in machine with <see cref="MachineId"/>
        /// </param>
        /// <param name="machineId">
        /// Id of machine which is sending this messege
        /// </param>
        public UdpMessage(UInt32 messageId, UInt16 protocolVersion, UInt16 tcpPort, String machineId)
            : base(messageId, machineId, protocolVersion)
        {
            TcpPort = tcpPort;
        }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                MessageId = reader.ReadUInt32();
                MachineId = reader.ReadString();

                ProtocolVersion = reader.ReadUInt16();
                TcpPort = reader.ReadUInt16();                

                return this;
            }
            else
            {
                throw new ArgumentNullException("ReaderNullException");
            }
        }

        /// <inheritdoc/>
        public override void Write(WireWriter writer)
        {
            if(writer != null)
            {
                writer.Write(MessageId);
                writer.Write(MachineId);
                writer.Write(ProtocolVersion);
                writer.Write(TcpPort);
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
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
