using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class PingResponse : Response 
    {
        public static void SendSameRandomId(SocketInConnectionPool sender, TimeSpan timeoutToSend, PingRequest request)
        {
            if (request?.RandomID != default)
            {
                var response = new StoreResponse
                {
                    RandomID = request.RandomID
                };

                sender.Send(response.ToByteArray(), timeoutToSend, out _);
            }
            else
            {
                throw new ArgumentException($"{nameof(request.RandomID)} is equal to {default(BigInteger)}");
            }
        }
    }
}
