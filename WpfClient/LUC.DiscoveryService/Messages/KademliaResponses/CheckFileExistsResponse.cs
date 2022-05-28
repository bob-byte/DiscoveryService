
using System;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class CheckFileExistsResponse : AbstractFileResponse
    {
        public CheckFileExistsResponse( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        public CheckFileExistsResponse( BigInteger requestRandomId )
            : base( requestRandomId )
        {
            DefaultInit();
        }

        protected override void DefaultInit( params Object[] args ) =>
            MessageOperation = MessageOperation.CheckFileExistsResponse;
    }
}
