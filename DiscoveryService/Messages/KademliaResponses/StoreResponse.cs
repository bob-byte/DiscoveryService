using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class StoreResponse : Response
    {
        public static void SendSameRandomId(Socket sender, TimeSpan timeoutToSend, StoreRequest request)
        {
            if (request?.RandomID != default)
            {
                var response = new StoreResponse
                {
                    RandomID = request.RandomID
                };

                sender.SendTimeout = timeoutToSend.Milliseconds;
                sender.Send(response.ToByteArray());
            }
            else
            {
                throw new ArgumentException($"{nameof(request.RandomID)} is equal to {default(BigInteger)}");
            }
        }
    }
}
