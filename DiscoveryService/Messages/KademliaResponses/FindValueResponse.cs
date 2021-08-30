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
        public ICollection<Contact> CloseContacts { get; set; }
        public String ValueInResponsingPeer { get; set; }

        public static void SendOurCloseContactsAndMachineValue(
            FindValueRequest request, 
            Socket sender, 
            IEnumerable<Contact> closeContacts, 
            TimeSpan timeoutToSend, 
            String machineValue)
        {
            if ((request?.RandomID != default) && (sender != null))
            {
                MessageOperation kadOperation;
                if(machineValue != null)
                {
                    kadOperation = MessageOperation.FindValueResponseWithValue;
                }
                else if(closeContacts != null)
                {
                    kadOperation = MessageOperation.FindValueResponseWithCloseContacts;
                }
                else
                {
                    throw new ArgumentNullException($"Both {nameof(closeContacts)} and {nameof(machineValue)} are equal to {null}");
                }

                var response = new FindValueResponse
                {
                    MessageOperation = kadOperation,
                    RandomID = request.RandomID,
                    CloseContacts = closeContacts?.ToList(),
                    ValueInResponsingPeer = machineValue
                };

                sender.SendTimeout = (Int32)timeoutToSend.TotalMilliseconds;
                sender.Send(response.ToByteArray());

                LogResponse(sender, response);
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
                base.Read(reader);

                if(MessageOperation == MessageOperation.FindValueResponseWithValue)
                {
                    ValueInResponsingPeer = reader.ReadString();
                }
                else if(MessageOperation == MessageOperation.FindValueResponseWithCloseContacts)
                {
                    CloseContacts = reader.ReadListOfContacts(Constants.LastSeenFormat);
                }

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
                base.Write(writer);

                if(ValueInResponsingPeer != null)
                {
                    writer.Write(ValueInResponsingPeer);
                }
                else if(CloseContacts != null)
                {
                    writer.WriteEnumerable(CloseContacts, Constants.LastSeenFormat);
                }
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
                                 $"{nameof(CloseContacts)}:");

                if(CloseContacts != null)
                {
                    foreach (var closeContact in CloseContacts)
                    {
                        writer.WriteLine($"Close contact: {closeContact}\n");
                    }
                }

                return writer.ToString();
            }
        }
    }
}
