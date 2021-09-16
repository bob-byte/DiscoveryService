using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia
{
    abstract class AbstractKademlia
    {
        protected readonly ClientKadOperation m_clientKadOperation;

        public AbstractKademlia( UInt16 protocolVersion )
        {
            m_clientKadOperation = new ClientKadOperation( protocolVersion );
        }
    }
}
