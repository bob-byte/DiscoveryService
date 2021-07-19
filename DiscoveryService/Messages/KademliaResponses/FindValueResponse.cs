using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class FindValueResponse : Response
    {
        public ICollection<Contact> CloseContactsToRepsonsingPeer { get; set; }
        public String ValueInResponsingPeer { get; set; }

        public static void SendOurCloseContactsAndMachineValue(FindValueRequest request, Socket sender, 
            IEnumerable<Contact> closeContacts, TimeSpan timeoutToSend, String machineValue)
        {
            if ((request?.RandomID != default) && (sender != null))
            {
                var response = new FindValueResponse
                {
                    RandomID = request.RandomID,
                    CloseContactsToRepsonsingPeer = closeContacts.ToList(),
                    ValueInResponsingPeer = machineValue
                };

                sender.SendTimeout = timeoutToSend.Milliseconds;
                sender.Send(response.ToByteArray());
                //response.Send(new IPEndPoint(remoteHost, (Int32)tcpPort)).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentNullException($"Any parameter(-s) in method {nameof(SendOurCloseContactsAndMachineValue)} is(are) null");
            }
        }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                RandomID = BigInteger.Parse(reader.ReadString());
                CloseContactsToRepsonsingPeer = reader.ReadListOfContacts();
                ValueInResponsingPeer = reader.ReadString();

                return this;
            }
            else
            {
                throw new ArgumentNullException("ReaderNullException");
            }
        }

        /// <inheritdoc/>
        public override void Write(WireWriter writer)
        {
            if (writer != null)
            {
                writer.Write(RandomID.ToString());
                writer.WriteEnumerable(CloseContactsToRepsonsingPeer);
                writer.Write(ValueInResponsingPeer);
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }
    }
}
