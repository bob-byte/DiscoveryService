using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class IDLengthException : Exception
    {
        public IDLengthException() { }
        public IDLengthException(string msg) : base(msg) { }
    }
}
