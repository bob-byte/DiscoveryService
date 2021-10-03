using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class ValueCannotBeNullException : Exception
    {
        public ValueCannotBeNullException() { }
        public ValueCannotBeNullException( String msg ) : base( msg ) { }
    }
}
