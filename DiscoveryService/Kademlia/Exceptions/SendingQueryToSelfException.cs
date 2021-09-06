using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class SendingQueryToSelfException : Exception
    {
        public SendingQueryToSelfException() { }
        public SendingQueryToSelfException(string msg) : base(msg) { }
    }
}
