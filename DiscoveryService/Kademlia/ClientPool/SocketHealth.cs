using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    enum SocketHealth
    {
        None = -1,
        Healthy = 0,
        IsNotConnected = 1,
        Expired = 2
    }
}
