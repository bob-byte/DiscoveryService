using System;

namespace LUC.DiscoveryServices.Kademlia.Exceptions
{
    public class SendingQueryToSelfException : Exception
    {
        public SendingQueryToSelfException() { }
        public SendingQueryToSelfException( String msg ) : base( msg ) { }
    }
}
