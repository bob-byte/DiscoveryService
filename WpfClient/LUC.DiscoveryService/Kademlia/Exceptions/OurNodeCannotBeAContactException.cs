using System;

namespace LUC.DiscoveryServices.Kademlia.Exceptions
{
    public class OurNodeCannotBeAContactException : Exception
    {
        public OurNodeCannotBeAContactException() { }
        public OurNodeCannotBeAContactException( String msg ) : base( msg ) { }
    }
}
