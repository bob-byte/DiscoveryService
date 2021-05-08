using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.CodingData
{
    enum PropertyInTcpMessage
    {
        First = 0,
        MessageId = 0,
        VersionOfProtocol = 1,
        GroupsSupported = 2,
        KnownIps = 3,
        Last = 3
    }
}
