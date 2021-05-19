using LUC.DiscoveryService.CodingData;
using System;
using System.Collections.Generic;
using System.IO;
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
        public TcpMessage(UInt32 messageId, UInt32 kadPort, UInt32 protocolVersion, List<String> groupsIds)
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

            KadPort = kadPort;
        }

        /// <summary>
        /// TCP port of the Kademilia service.
        /// </summary>
        public UInt32 KadPort { get; set; }

        /// <summary>
        /// Names of groups
        /// </summary>
        public List<String> GroupIds { get; set; }

        public override IWireSerialiser Read(WireReader reader)
        {
            if(reader != null)
            {
                MessageId = reader.ReadUInt32();
                ProtocolVersion = reader.ReadUInt32();
                KadPort = reader.ReadUInt32();
                GroupIds = reader.ReadListOfStrings();

                return this;
            }
            else
            {
                throw new ArgumentNullException(nameof(reader));
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
                writer.Write(MessageId);
                writer.Write(ProtocolVersion);
                writer.Write(KadPort);
                writer.WriteEnumerable(GroupIds);
            }
            else
            {
                throw new ArgumentNullException(nameof(writer));
            }
        }

        public override string ToString()
        {
            using(var writer = new StringWriter())
            {
                writer.WriteLine("TCP message:");
                writer.WriteLine($"{base.ToString()};");
                writer.WriteLine($"TCP port of the Kademilia service = {KadPort};");

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
