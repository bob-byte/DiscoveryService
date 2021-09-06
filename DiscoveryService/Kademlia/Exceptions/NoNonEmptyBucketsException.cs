using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class NoNonEmptyBucketsException : Exception
    {
        public NoNonEmptyBucketsException() { }
        public NoNonEmptyBucketsException(string msg) : base(msg) { }
    }
}
