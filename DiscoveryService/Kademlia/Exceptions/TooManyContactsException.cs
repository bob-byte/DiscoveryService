using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class TooManyContactsException : Exception
    {
        public TooManyContactsException() { }
        public TooManyContactsException(string msg) : base(msg) { }
    }
}
