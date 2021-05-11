using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LUC.DiscoveryService.Messages
{
    public class TcpMessage : Message
    {
        /// <summary>
        ///   Supported version of protocol of the remote application.
        /// </summary>
	public UInt32 ProtocolVersion { get; set;  };

        /// <summary>
        ///   The list group IDs, for example:
	///   the-light-test1-res, the-light-test2-res, etc
        /// </summary>
        public List<String> GroupIds { get; set;  } = new List<String>();

        public override IWireSerialiser Read(WireReader reader)
        {
            MessageId = reader.ReadUInt32();
	    ProtocolVersion = reader.ReadUInt32();
	    GroupIds = reader.ReadArray();
            return this;
        }

        public override string ToString()
        {
            using (var s = new StringWriter())
            {
                s.Write("Message");
                s.WriteLine();
                if (MessageId) s.Write("MessageId %d", MessageId);
                s.WriteLine();
                if (ProtocolVersion) s.Write("ProtocolVersion %d", ProtocolVersion);
                s.WriteLine();

                s.Write("Groups:");
                s.WriteLine();
                if (GroupIds.Count == 0)
                {
                    s.WriteLine("(empty)");
                }
                else
                {
                    foreach (var g in GroupIds)
                    {
                        s.WriteLine(g);
                    }
                }
                return s.ToString();
            }
        }
    }
}
