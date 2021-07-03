using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    abstract class Request : TcpMessage
    {
        public BigInteger RandomID { get; set; }
        public BigInteger Sender { get; set; }

        public Request()
        {
            RandomID = ID.RandomID.Value;
        }

        public void Send(ID keyToFindContact)
        {
            var remoteContact = DiscoveryService.KnownContacts.Single(c => c.ID == keyToFindContact);
            Send(new IPEndPoint(remoteContact.IPAddress, (Int32)remoteContact.TcpPort)).Wait();
        }


        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                RandomID = BigInteger.Parse(reader.ReadString());
                Sender = BigInteger.Parse(reader.ReadString());

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
                writer.Write(Sender.ToString());
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }
    }
}
