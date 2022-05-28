using System;

namespace LUC.DiscoveryServices.Kademlia
{
    internal abstract class AbstractKademlia
    {
        protected readonly KadLocalTcp m_remoteProcedureCaller;

        protected AbstractKademlia( UInt16 protocolVersion )
        {
            m_remoteProcedureCaller = new KadLocalTcp( protocolVersion );
        }
    }
}
