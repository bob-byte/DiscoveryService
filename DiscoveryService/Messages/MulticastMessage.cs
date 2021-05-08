using System;

namespace LUC.DiscoveryService.Messages
{
    public class MulticastMessage : Message
    {
        public MulticastMessage()
        {

        }

        public MulticastMessage(UInt32 messageId, String machineId, UInt32 tcpPort)
            : base(messageId)
        {
            MachineId = machineId;
            TcpPort = tcpPort;
            VersionOfProtocol = ProtocolVersion;
        }

        public MulticastMessage(UInt32 messageId, String machineId, UInt32 tcpPort, UInt32 receivedProtocolVersion)
            : base(messageId)
        {
            MachineId = machineId;
            TcpPort = tcpPort;
            VersionOfProtocol = receivedProtocolVersion;
        }

        public UInt32 TcpPort { get; set; }

        public String MachineId { get; set; }
    }
}
