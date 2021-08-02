using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia
{
    public abstract class AbstractKademlia
    {
        protected readonly ClientKadOperation clientKadOperation = new ClientKadOperation();
    }
}
