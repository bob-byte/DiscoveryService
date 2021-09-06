using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class ValueCannotBeNullException : Exception
    {
        public ValueCannotBeNullException() { }
        public ValueCannotBeNullException(string msg) : base(msg) { }
    }
}
