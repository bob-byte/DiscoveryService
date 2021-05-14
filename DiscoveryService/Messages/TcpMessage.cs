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
        public TcpMessage(UInt32 messageId, UInt32 tcpPort, List<String> groupsIds)
            : base(messageId, tcpPort)
        {
            if(groupsIds != null)
            {
                GroupsIds = groupsIds;
            }
            else
            {
                GroupsIds = new List<String>();
            }
        }

        /// <summary>
        /// Names of groups
        /// </summary>
        public List<String> GroupsIds { get; set; }

        public override IWireSerialiser Read(WireReader reader)
        {
            if(reader != null)
            {
                MessageId = reader.ReadUInt32();
                VersionOfProtocol = reader.ReadUInt32();
                TcpPort = reader.ReadUInt32();
                GroupsIds = reader.ReadStringList();

                return this;
            }
            else
            {
                throw new ArgumentNullException(nameof(reader));
            }
        }

        /// <summary>
        /// 
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
                writer.Write(VersionOfProtocol);
                writer.Write(TcpPort);
                writer.WriteEnumerable(GroupsIds);
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
                writer.WriteLine(base.ToString());

                writer.WriteLine($"{nameof(GroupsIds)}:");
                for (Int32 id = 0; id < GroupsIds.Count; id++)
                {
                    if(id == GroupsIds.Count - 1)
                    {
                        writer.WriteLine($"{GroupsIds[id]}");
                    }
                    else
                    {
                        writer.WriteLine($"{GroupsIds[id]};");
                    }
                }

                return writer.ToString();
            }
        }
    }
}
