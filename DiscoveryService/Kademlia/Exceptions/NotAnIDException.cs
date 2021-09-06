using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class NotAnIDException : Exception
    {
        public NotAnIDException() { }
        public NotAnIDException(string msg) : base(msg) { }
    }
}
