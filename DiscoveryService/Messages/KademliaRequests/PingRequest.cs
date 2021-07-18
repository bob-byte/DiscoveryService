using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    public class PingRequest : Request
    {
        public PingRequest()
            : base()
        {
            ;//do nothing
        }

        public PingRequest(UInt32 tcpPort)
            : base(tcpPort)
        {
            ;//do nothing
        }

        public override string ToString()
        {
            using (var writer = new StringWriter())
            {
                writer.Write($"{GetType()}:\n" +
                             $"Random ID = {RandomID}");

                return writer.ToString();
            }
        }
    }
}
