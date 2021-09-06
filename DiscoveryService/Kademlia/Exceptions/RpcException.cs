using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class RpcException : Exception
    {
        public RpcException() { }
        public RpcException(string msg) : base(msg) { }
    }
}
