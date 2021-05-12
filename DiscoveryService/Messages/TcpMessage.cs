using LUC.DiscoveryService.CodingData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LUC.DiscoveryService.Messages
{
    public class TcpMessage : Message
    {
        public TcpMessage(UInt32 messageId, UInt32 receivedProcolVersion, List<String> groupsIds)
            : base(messageId)
        {
            GroupsIds = groupsIds;
            VersionOfProtocol = receivedProcolVersion;
        }

        public List<String> GroupsIds { get; set; }

        public override IWireSerialiser Read(WireReader reader)
        {
            if(reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            MessageId = reader.ReadUInt32();
            VersionOfProtocol = reader.ReadUInt32();
            GroupsIds = reader.ReadEnumerableOfString().ToList();
            return this;
        }

        public override void Write(WireWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            writer.Write(MessageId);
            writer.Write(VersionOfProtocol);
            writer.WriteEnumerable(GroupsIds);
        }

        public override string ToString()
        {
            using(var writer = new StringWriter())
            {
                writer.WriteLine("TCP message\n");
                writer.Write($"MessageId = {MessageId};\n" +
                             $"Protocol version = {VersionOfProtocol}\n");

                foreach (var groupId in GroupsIds)
                {
                    writer.Write($"{groupId};\n");
                }

                return writer.ToString();
            }
        }
    }
}
