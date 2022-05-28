using System;

namespace LUC.DiscoveryServices.Kademlia.Exceptions
{
    public class NotContactException : Exception
    {
        public NotContactException()
            : base()
        {
            ;//do nothing
        }

        public NotContactException( String messageException )
            : base( messageException )
        {
            ;//do nothing
        }
    }
}
