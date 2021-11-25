using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    enum SocketStateInPool
    {
        NeverWasInPool,
        TakenFromPool,
        //DeletedFromPool,
        IsFailed,
        IsInPool
    }
}
