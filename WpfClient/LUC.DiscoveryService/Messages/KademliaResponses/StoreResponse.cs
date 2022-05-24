
using System;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class StoreResponse : Response
    {
        public StoreResponse( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        public StoreResponse( BigInteger requestRandomId )
            : base( requestRandomId )
        {
            DefaultInit();
        }

        protected override void DefaultInit( params Object[] args ) =>
            MessageOperation = MessageOperation.StoreResponse;
    }
}
