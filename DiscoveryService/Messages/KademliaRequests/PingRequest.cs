using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages.KademliaRequests
{
    class PingRequest : Request
    {
        public PingRequest( BigInteger senderKadId, String senderMachineId )
            : base( senderKadId, senderMachineId )
        {
            DefaultInit();
        }

        public PingRequest()
        {
            DefaultInit();
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.Ping;
    }
}
