using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class NotContactException : Exception
    {
        public NotContactException() { }
        public NotContactException(string msg) : base(msg) { }
    }
}
