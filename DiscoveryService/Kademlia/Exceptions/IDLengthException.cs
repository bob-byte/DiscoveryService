using System;

namespace LUC.DiscoveryServices.Kademlia.Exceptions
{
    public class IDLengthException : Exception
    {
        public IDLengthException()
               : base()
        {
            ;//do nothing
        }

        public IDLengthException( String messageException )
            : base( messageException )
        {
            ;//do nothing
        }
    }
}
