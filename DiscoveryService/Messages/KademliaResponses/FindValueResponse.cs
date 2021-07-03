using LUC.DiscoveryService.Kademlia;
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

        public static void SendOurCloseContactsAndMachineId(FindValueRequest request, IPAddress remoteHost, IEnumerable<Contact> closeContacts, String machineId, UInt32 tcpPort)
        {
            if ((request != null) && (remoteHost != null))
            {
                var response = new FindNodeResponse
                {
                    RandomID = request.RandomID,
                    Contacts = closeContacts.ToList(),
                    TcpPort = tcpPort,
                    MachineId = machineId
                };
                
                response.Send(new IPEndPoint(remoteHost, (Int32)tcpPort)).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentNullException($"Any parameter(-s) in method {nameof(SendOurCloseContactsAndMachineId)} is(are) null");
            }
        }
    }
}
