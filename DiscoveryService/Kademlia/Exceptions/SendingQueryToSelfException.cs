using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class SendingQueryToSelfException : Exception
    {
        public SendingQueryToSelfException() { }
        public SendingQueryToSelfException( String msg ) : base( msg ) { }
    }
}
