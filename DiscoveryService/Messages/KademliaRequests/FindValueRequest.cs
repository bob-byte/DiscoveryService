using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class FindValueRequest : Request
    {
        public BigInteger IdOfContact { get; set; }

        public FindValueRequest()
            : base()
        {
            ;//do nothing
        }

        public FindValueRequest(UInt32 tcpPort)
            : base(tcpPort)
        {
            ;//do nothing
        }
    }
}
