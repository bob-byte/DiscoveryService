using System;

namespace LUC.DiscoveryService.Messages
{
    public class MulticastMessage : Message
    {
        public MulticastMessage(String id, Int32 tcpPort)
        {
            MachineId = id;
            TcpPort = tcpPort;
            VersionOfProtocol = ProtocolVersion;
        }

        public MulticastMessage(String id, Int32 tcpPort, Int32 receivedProtocolVersion)
        {
            MachineId = id;
            TcpPort = tcpPort;
            VersionOfProtocol = receivedProtocolVersion;
        }

        public Int32 TcpPort { get; set; }

        public String MachineId { get; set; }
    }
}
