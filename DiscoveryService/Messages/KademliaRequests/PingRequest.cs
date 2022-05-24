using System;
using System.Numerics;

namespace DiscoveryServices.Messages.KademliaRequests
{
    class PingRequest : Request
    {
        public PingRequest( BigInteger senderKadId, String senderMachineId )
            : base( senderKadId, senderMachineId )
        {
            DefaultInit();
        }

        public PingRequest( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        protected override void DefaultInit( params Object[] args ) =>
            MessageOperation = MessageOperation.Ping;
    }
}
