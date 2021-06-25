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
        /// Create a new instance of the <see cref="MulticastMessage"/> class. This constructor is often used to write message to a stream
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
        public MulticastMessage(UInt32 messageId, UInt32 protocolVersion, UInt32 tcpPort, ID machineId)
            : base(messageId, protocolVersion)
        {
            TcpPort = tcpPort;
            MachineId = machineId;
        }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                MessageId = reader.ReadUInt32();

                var idAsBigInt = BigInteger.Parse(reader.ReadString());
                MachineId = new ID(idAsBigInt);

                ProtocolVersion = reader.ReadUInt32();
                TcpPort = reader.ReadUInt32();                

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
                writer.WriteString(MachineId.Value.ToString());
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
