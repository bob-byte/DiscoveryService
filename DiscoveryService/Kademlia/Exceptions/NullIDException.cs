using System;

namespace DiscoveryServices.Kademlia.Exceptions
{
    public class NullIDException : Exception
    {
        public NullIDException() { }
        public NullIDException( String msg ) : base( msg ) { }
    }
}
