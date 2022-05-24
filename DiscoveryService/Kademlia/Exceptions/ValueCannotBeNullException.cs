using System;

namespace DiscoveryServices.Kademlia.Exceptions
{
    public class ValueCannotBeNullException : Exception
    {
        public ValueCannotBeNullException() { }
        public ValueCannotBeNullException( String msg ) : base( msg ) { }
    }
}
