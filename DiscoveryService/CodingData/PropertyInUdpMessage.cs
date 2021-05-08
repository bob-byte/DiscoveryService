using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.CodingData
{
    enum PropertyInUdpMessage
    {
        First = 0,
        MessageId = 0,
        MachineId = 1,
        VersionOfProtocol = 2,
        TcpPort = 3,
        Last = 3
    }
}
