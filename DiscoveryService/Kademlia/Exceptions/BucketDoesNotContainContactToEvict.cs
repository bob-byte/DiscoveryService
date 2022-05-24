using System;

namespace DiscoveryServices.Kademlia.Exceptions
{
    public class BucketDoesNotContainContactToEvictException : InvalidOperationException
    {
        public BucketDoesNotContainContactToEvictException()
        {
            ;//do nothing
        }

        public BucketDoesNotContainContactToEvictException( String msg )
            : base( msg )
        {
            ;//do nothing
        }
    }
}
