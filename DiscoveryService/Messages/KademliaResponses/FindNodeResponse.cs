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
    class FindNodeResponse : Response
    {
        public ICollection<Contact> Contacts { get; set; }

        public static void SendOurCloseContactsAndPort(Socket sender, IEnumerable<Contact> closeContactsToLocalContactId, 
            TimeSpan timeoutToSend, FindNodeRequest message)
        {
            if (message?.RandomID != default)
            {
                var response = new FindNodeResponse
                {
                    MessageOperation = MessageOperation.FindNodeResponse,
                    RandomID = message.RandomID,
                    Contacts = closeContactsToLocalContactId.ToList(),
                };

                sender.SendTimeout = (Int32)timeoutToSend.TotalMilliseconds;
                sender.Send(response.ToByteArray());
                //response.Send(new IPEndPoint(iPEndPoint.Address, (Int32)message.TcpPort), response.ToByteArray()).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException($"Bad format of {nameof(message)}");
            }
        }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                MessageOperation = (MessageOperation)reader.ReadUInt32();
                RandomID = BigInteger.Parse(reader.ReadString());
                Contacts = reader.ReadListOfContacts();

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
                writer.Write((UInt32)MessageOperation);
                writer.Write(RandomID.ToString());
                writer.WriteEnumerable(Contacts);
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }
    }
}
