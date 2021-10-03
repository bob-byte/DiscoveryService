using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class OurNodeCannotBeAContactException : Exception
    {
        public OurNodeCannotBeAContactException() { }
        public OurNodeCannotBeAContactException( String msg ) : base( msg ) { }
    }
}
