using DeviceId;
using DiscoveryServices.Extensions;
using DiscoveryServices.Extensions.IPExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscoveryServices.Messages
{
    class BroadcastMessage : Message
    {
        public Int32 TcpPort { get; set; } = 17500;

        public String Id { get; set; }

        public BroadcastMessage(String id, Int32 tcpPort)
        {
            Id = id;
            TcpPort = tcpPort;
            VersionOfProtocol = ProtocolVersion;
        }

        public BroadcastMessage(String id, Int32 tcpPort, Int32 receivedProcolVersion)
        {
            Id = id;
            TcpPort = tcpPort;
            VersionOfProtocol = receivedProcolVersion;
        }
    }
}
