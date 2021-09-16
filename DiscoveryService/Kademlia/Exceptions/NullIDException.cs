using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class NullIDException : Exception
    {
        public NullIDException() { }
        public NullIDException( String msg ) : base( msg ) { }
    }
}
