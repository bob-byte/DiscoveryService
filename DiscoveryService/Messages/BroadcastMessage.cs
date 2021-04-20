using System;

namespace LUC.DiscoveryService.Messages
{
    class BroadcastMessage : Message
    {
        public Int32 TcpPort { get; set; }

        public String MachineId { get; set; }

        public BroadcastMessage(String id, Int32 tcpPort)
        {
            MachineId = id;
            TcpPort = tcpPort;
            VersionOfProtocol = ProtocolVersion;
        }

        public BroadcastMessage(String id, Int32 tcpPort, Int32 receivedProcolVersion)
        {
            MachineId = id;
            TcpPort = tcpPort;
            VersionOfProtocol = receivedProcolVersion;
        }
    }
}
