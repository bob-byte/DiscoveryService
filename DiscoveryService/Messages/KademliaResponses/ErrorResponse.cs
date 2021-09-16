using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class ErrorResponse : Response
    {
        public ErrorResponse( BigInteger randomId )
            : base( randomId )
        {
            DefaultInit();
        }

        public String ErrorMessage { get; set; }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.ErrorResponse;
    }
}
