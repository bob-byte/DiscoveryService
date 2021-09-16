using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class PingRequest : Request
    {
        public PingRequest( BigInteger sender )
            : base( sender )
        {
            DefaultInit();
        }

        public PingRequest()
        {
            DefaultInit();
        }

        public override String ToString()
        {
            using ( StringWriter writer = new StringWriter() )
            {
                writer.Write( $"{GetType().Name}:\n" +
                              $"Random ID = {RandomID}" );

                return writer.ToString();
            }
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.Ping;
    }
}
