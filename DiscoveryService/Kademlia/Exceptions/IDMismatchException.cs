using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class IDMismatchException : Exception
    {
        public IDMismatchException() { }
        public IDMismatchException(string msg) : base(msg) { }
    }
}
