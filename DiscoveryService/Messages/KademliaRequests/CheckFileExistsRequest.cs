using LUC.DiscoveryServices.CodingData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages.KademliaRequests
{
    class CheckFileExistsRequest : AbstractFileRequest
    {
        public CheckFileExistsRequest( BigInteger senderKadId, String senderMachineId )
            : base( senderKadId, senderMachineId )
        {
            DefaultInit();
        }

        public CheckFileExistsRequest()
        {
            DefaultInit();
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.CheckFileExists;
    }
}
