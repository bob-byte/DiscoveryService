using System;

namespace LUC.DiscoveryService.Kademlia.Exceptions
{
    public class RpcException : Exception
    {
        public RpcException() { }
        public RpcException( String msg ) : base( msg ) { }
    }
}
