using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia
{
    abstract class AbstractKademlia
    {
        protected readonly ClientKadOperation clientKadOperation;

        public AbstractKademlia(UInt16 protocolVersion)
        {
            clientKadOperation = new ClientKadOperation(protocolVersion);
        }
    }
}
