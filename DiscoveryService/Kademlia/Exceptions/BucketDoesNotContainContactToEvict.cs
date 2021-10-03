using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class BucketDoesNotContainContactToEvict : Exception
    {
        public BucketDoesNotContainContactToEvict()
        {
            ;//do nothing
        }

        public BucketDoesNotContainContactToEvict( String msg )
            : base( msg )
        {
            ;//do nothing
        }
    }
}
