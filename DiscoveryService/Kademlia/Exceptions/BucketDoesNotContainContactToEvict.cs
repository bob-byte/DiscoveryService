using System;

namespace LUC.DiscoveryServices.Kademlia.Exceptions
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
