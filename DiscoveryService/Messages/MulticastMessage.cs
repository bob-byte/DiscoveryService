using System;

namespace LUC.DiscoveryService.Messages
{
    public class MulticastMessage : Message
    {
        /// <summary>
        ///   Unique machine ID within LAN.
        /// </summary>
	public String MachineId { get; set;  };

        /// <summary>
        ///   TCP port the remote side listening on for receiving information
	//    on IP: groups.
        /// </summary>
	public UInt32 TcpPort { get; set;  };

        /// <summary>
        ///   Supported version of protocol of the remote application.
        /// </summary>
	public UInt32 ProtocolVersion { get; set;  };

        public override IWireSerialiser Read(WireReader reader)
        {
            MessageId = reader.ReadUInt32();
            String = reader.ReadString();
	    TcpPort = reader.ReadUInt32();
	    ProtocolVersion = reader.ReadUInt32();
            return this;
        }

        public override string ToString()
        {
            using (var s = new StringWriter())
            {
                s.Write("Message");
                s.WriteLine();
                if (MessageId) s.Write("MessageId %d", MessageId);
                if (MachineId) s.Write("MachineId %s", MachineId);
                if (TcpPort) s.Write("TcpPort %d", TcpPort);
                if (ProtocolVersion) s.Write("ProtocolVersion %d", ProtocolVersion);
                s.WriteLine();
                return s.ToString();
            }
        }
    }
}
