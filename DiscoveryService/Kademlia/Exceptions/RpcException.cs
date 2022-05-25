using System;

namespace LUC.DiscoveryServices.Kademlia.Exceptions
{
    public class RpcException : Exception
    {
        public RpcException() { }
        public RpcException( String msg ) : base( msg ) { }
    }
}
