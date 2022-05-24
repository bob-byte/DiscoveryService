using System;

namespace DiscoveryServices.Kademlia.Exceptions
{
    public class IDMismatchException : Exception
    {
        public IDMismatchException()
            : base()
        {
            ;//do nothing
        }

        public IDMismatchException( String messageException )
            : base( messageException )
        {
            ;//do nothing
        }
    }
}
