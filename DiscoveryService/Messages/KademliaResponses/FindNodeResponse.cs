﻿using LUC.DiscoveryService.CodingData;
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
    class FindNodeResponse : Response
    {
        public FindNodeResponse()
        {
            MessageOperation = MessageOperation.FindNodeResponse;
        }

        public ICollection<Contact> CloseSenderContacts { get; set; }

        public static void SendOurCloseContacts(Socket sender, IEnumerable<Contact> closeContactsToLocalContactId, 
            TimeSpan timeoutToSend, FindNodeRequest message)
        {
            if (message?.RandomID != default)
            {
                var response = new FindNodeResponse
                {
                    MessageOperation = MessageOperation.FindNodeResponse,
                    RandomID = message.RandomID,
                    CloseSenderContacts = closeContactsToLocalContactId.ToList(),
                };

                sender.SendTimeout = (Int32)timeoutToSend.TotalMilliseconds;
                var bytesOfReponse = response.ToByteArray();
                sender.Send(bytesOfReponse);

                LogResponse(sender, response);
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
                base.Read(reader);
                CloseSenderContacts = reader.ReadListOfContacts(Constants.LastSeenFormat);

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
                writer.WriteEnumerable(CloseSenderContacts, Constants.LastSeenFormat);
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
                                 $"{nameof(CloseSenderContacts)}:");

                foreach (var closeContact in CloseSenderContacts)
                {
                    writer.WriteLine($"Close contact: {closeContact}\n");
                }

                return writer.ToString();
            }
        }
    }
}
