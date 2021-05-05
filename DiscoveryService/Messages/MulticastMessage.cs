using System;

namespace LUC.DiscoveryService.Messages
{
    public class MulticastMessage : Message
    {
        public MulticastMessage(Int32 messageId, String machineId, Int32 tcpPort)
            : base(messageId)
        {
            MachineId = machineId;
            TcpPort = tcpPort;
            VersionOfProtocol = ProtocolVersion;
        }

        public MulticastMessage(Int32 messageId, String machineId, Int32 tcpPort, Int32 receivedProtocolVersion)
            : base(messageId)
        {
            MachineId = machineId;
            TcpPort = tcpPort;
            VersionOfProtocol = receivedProtocolVersion;
        }

        public Int32 TcpPort { get; set; }

        public String MachineId { get; set; }
    }
}
