using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Generic;
using System.IO;
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
                    MessageOperation = MessageOperation.FindValueResponse,
                    RandomID = request.RandomID,
                    CloseContactsToRepsonsingPeer = closeContacts.ToList(),
                    ValueInResponsingPeer = machineValue
                };

                sender.SendTimeout = (Int32)timeoutToSend.TotalMilliseconds;
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
                MessageOperation = (MessageOperation)reader.ReadUInt32();
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
                writer.Write((UInt32)MessageOperation);
                writer.Write(RandomID.ToString());
                writer.WriteEnumerable(CloseContactsToRepsonsingPeer);
                writer.Write(ValueInResponsingPeer);
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }

        public override String ToString()
        {
            using (var writer = new StringWriter())
            {
                writer.WriteLine($"{GetType().Name}:\n" +
                                 $"{PropertyWithValue(nameof(RandomID), RandomID)};\n" +
                                 $"{PropertyWithValue(nameof(ValueInResponsingPeer), ValueInResponsingPeer)};\n" +
                                 $"{nameof(CloseContactsToRepsonsingPeer)}:");

                foreach (var closeContact in CloseContactsToRepsonsingPeer)
                {
                    writer.WriteLine($"{closeContact};\n");
                }

                return writer.ToString();
            }
        }
    }
}
