using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class NotAnIDException : Exception
    {
        public NotAnIDException()
            : base()
        {
            ;//do nothing
        }

        public NotAnIDException( String messageException )
            : base( messageException )
        {
            ;//do nothing
        }
    }
}
