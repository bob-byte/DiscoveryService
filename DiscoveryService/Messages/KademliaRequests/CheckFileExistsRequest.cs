using LUC.DiscoveryService.CodingData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class CheckFileExistsRequest : AbstractFileRequest
    {
        public CheckFileExistsRequest( BigInteger sender )
            : base( sender )
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
