using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class BucketDoesNotContainContactToEvict : Exception
    {
        public BucketDoesNotContainContactToEvict() { }
        public BucketDoesNotContainContactToEvict(string msg) : base(msg) { }
    }
}
