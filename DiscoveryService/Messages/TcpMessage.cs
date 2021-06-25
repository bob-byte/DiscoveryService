using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    /// Allows to write and read TCP message to/from <see cref="Stream"/>
    /// </summary>
    public class TcpMessage : Message
    {
        /// <summary>
        /// Create a new instance of the <see cref="TcpMessage"/> class. This constructor is often used to read message
        /// </summary>
        public TcpMessage()
        {
            ;
        }

        /// <summary>
        ///   Create a new instance of the <see cref="TcpMessage"/> class. This constructor is often used to write message to a stream
        /// </summary>
        /// <param name="messageId">
        ///   Unique message identifier. It is used to detect duplicate messages.
        /// </param>
        /// /// <param name="groupsIds">
        /// Names of user groups
        /// </param>
        /// <param name="tcpPort">
        /// TCP port for Kademilia requests.
        /// </param>
        public TcpMessage(UInt32 messageId, UInt32 tcpPort, UInt32 protocolVersion, List<String> groupsIds)
            : base(messageId, protocolVersion)
        {
            if(groupsIds != null)
            {
                GroupIds = groupsIds;
            }
            else
            {
                GroupIds = new List<String>();
            }

            TcpPort = tcpPort;
        }

        /// <summary>
        ///   The kind of message.
        /// </summary>
        /// <value>
        ///   Defaults to <see cref="MessageOperation.Acknowledge"/>.
        /// </value>
        public MessageOperation Opcode { get; set; } = MessageOperation.Acknowledge;

        /// <summary>
        /// Names of groups
        /// </summary>
        public List<String> GroupIds { get; set; }

        public override IWireSerialiser Read(WireReader reader)
        {
            if(reader != null)
            {
                Opcode = (MessageOperation)reader.ReadByte();
                MessageId = reader.ReadUInt32();

                var idAsBigInt = BigInteger.Parse(reader.ReadString());
                MachineId = new ID(idAsBigInt);

                ProtocolVersion = reader.ReadUInt32();
                TcpPort = reader.ReadUInt32();
                GroupIds = reader.ReadListOfStrings();

                return this;
            }
            else
            {
                throw new ArgumentNullException("ReaderNullException");
            }
        }

        /// <summary>
        /// Write as a binary message.
        /// </summary>
        /// <param name="writer"></param>
        /// <exception cref="ArgumentNullException">
        /// 
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// 
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// 
        /// </exception>
        public override void Write(WireWriter writer)
        {
            if (writer != null)
            {
                writer.WriteByte((Byte)Opcode);
                writer.Write(MessageId);
                writer.WriteString(MachineId.Value.ToString());
                writer.Write(ProtocolVersion);
                writer.Write(TcpPort);
                writer.WriteEnumerable(GroupIds);
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }

        public override string ToString()
        {
            using(var writer = new StringWriter())
            {
                writer.WriteLine($"TCP message:\n" +
                    $"Message operation: {Opcode};");
                writer.WriteLine($"{base.ToString()};");
                writer.WriteLine($"TCP port = {TcpPort};");

                writer.WriteLine($"{nameof(GroupIds)}:");
                for (Int32 id = 0; id < GroupIds.Count; id++)
                {
                    if(id == GroupIds.Count - 1)
                    {
                        writer.WriteLine($"{GroupIds[id]}");
                    }
                    else
                    {
                        writer.WriteLine($"{GroupIds[id]};");
                    }
                }

                return writer.ToString();
            }
        }
    }
}
