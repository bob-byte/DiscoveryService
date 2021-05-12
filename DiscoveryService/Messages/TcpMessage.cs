using LUC.DiscoveryService.CodingData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LUC.DiscoveryService.Messages
{
    public class TcpMessage : Message
    {
        public TcpMessage()
        {
            ;
        }

        /// <summary>
        ///   Create a new instance of the <see cref="TcpMessage"/> class.
        /// </summary>
        /// <param name="messageId">
        ///   Unique message identifier. It is used to detect duplicate messages.
        /// </param>
        public TcpMessage(UInt32 messageId, UInt32 receivedProcolVersion, List<String> groupsIds)
            : base(messageId)
        {
            if(groupsIds == null)
            {
                GroupsIds = new List<String>();
            }
            else
            {
                GroupsIds = groupsIds;
            }

            VersionOfProtocol = receivedProcolVersion;
        }

        /// <summary>
        /// Names of groups
        /// </summary>
        public List<String> GroupsIds { get; set; }

        public override IWireSerialiser Read(WireReader reader)
        {
            if(reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            else
            {
                MessageId = reader.ReadUInt32();
                VersionOfProtocol = reader.ReadUInt32();
                GroupsIds = reader.ReadStringList();

                return this;
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
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            else
            {
                writer.Write(MessageId);
                writer.Write(VersionOfProtocol);
                writer.WriteEnumerable(GroupsIds);
            }
        }

        public override string ToString()
        {
            using(var writer = new StringWriter())
            {
                writer.WriteLine("TCP message:");
                writer.WriteLine($"MessageId = {MessageId};\n" +
                                 $"Protocol version = {VersionOfProtocol};");

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
