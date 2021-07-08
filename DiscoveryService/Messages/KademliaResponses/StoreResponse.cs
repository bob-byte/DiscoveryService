using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class StoreResponse : Response
    {
        public static void SendSameRandomId(DiscoveryServiceSocket sender, TimeSpan timeoutToSend, StoreRequest request)
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
