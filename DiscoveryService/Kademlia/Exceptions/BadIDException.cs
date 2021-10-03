using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class BadIDException : Exception
    {
        public BadIDException()
        {
            ;//do nothing
        }
        public BadIDException( String msg )
            : base( msg )
        {
            ;//do nothing
        }
    }
}
