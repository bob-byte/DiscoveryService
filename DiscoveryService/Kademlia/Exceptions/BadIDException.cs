using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class BadIDException : Exception
    {
        public BadIDException() { }
        public BadIDException(string msg) : base(msg) { }
    }
}
