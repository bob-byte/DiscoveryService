using System;

namespace DiscoveryServices.Kademlia.Exceptions
{
    public class TooManyContactsException : Exception
    {
        public TooManyContactsException() { }
        public TooManyContactsException( String msg ) : base( msg ) { }
    }
}
