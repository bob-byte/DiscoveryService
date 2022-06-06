using System;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class PingResponse : Response
    {
        public PingResponse( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        public PingResponse( BigInteger requestRandomId )
            : base( requestRandomId )
        {
            DefaultInit();
        }

        protected override void DefaultInit( params Object[] args ) =>
            MessageOperation = MessageOperation.PingResponse;
    }
}
