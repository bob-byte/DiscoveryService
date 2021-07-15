using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class FindValueResponse : Response
    {
        public List<Contact> CloseContactsToRepsonsingPeer { get; set; }
        public String ValueInResponsingPeer { get; set; }

        public static void SendOurCloseContactsAndMachineValue(FindValueRequest request, SocketInConnectionPool sender, 
            IEnumerable<Contact> closeContacts, String machineValue)
        {
            if ((request?.RandomID != default) && (sender != null))
            {
                var response = new FindValueResponse
                {
                    RandomID = request.RandomID,
                    CloseContactsToRepsonsingPeer = closeContacts.ToList(),
                    ValueInResponsingPeer = machineValue
                };

                sender.Send(response.ToByteArray(), Constants.SendTimeout, out _);
                //response.Send(new IPEndPoint(remoteHost, (Int32)tcpPort)).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentNullException($"Any parameter(-s) in method {nameof(SendOurCloseContactsAndMachineValue)} is(are) null");
            }
        }
    }
}
