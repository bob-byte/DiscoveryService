using LUC.DiscoveryService.Kademlia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService
{
    class OnlyInNetwork
    {
        public static Boolean IsOnlyInNetwork(ID ourContactId, ID remoteContactId) =>
            ourContactId == remoteContactId;
    }
}
