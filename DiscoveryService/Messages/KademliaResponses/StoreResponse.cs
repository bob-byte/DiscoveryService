﻿using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Generic;
using System.IO;
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
                    MessageOperation = MessageOperation.StoreResponse,
                    RandomID = request.RandomID
                };

                sender.SendTimeout = (Int32)timeoutToSend.TotalMilliseconds;
                sender.Send(response.ToByteArray());
            }
            else
            {
                throw new ArgumentException($"{nameof(request.RandomID)} is equal to {default(BigInteger)}");
            }
        }

        public override String ToString()
        {
            using (var writer = new StringWriter())
            {
                writer.WriteLine($"{GetType().Name}:\n" +
                                 $"{PropertyWithValue(nameof(RandomID), RandomID)}");

                return writer.ToString();
            }
        }
    }
}
