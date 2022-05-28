
using System;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages.KademliaRequests
{
    class CheckFileExistsRequest : AbstractFileRequest
    {
        public CheckFileExistsRequest( BigInteger senderKadId, String senderMachineId )
            : base( senderKadId, senderMachineId )
        {
            DefaultInit();
        }

        public CheckFileExistsRequest( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        protected override void DefaultInit( params Object[] args ) =>
            MessageOperation = MessageOperation.CheckFileExists;
    }
}
