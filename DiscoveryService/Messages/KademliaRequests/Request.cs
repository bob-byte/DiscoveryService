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
    public abstract class Request : Message
    {
        public BigInteger RandomID { get; set; }
        public BigInteger Sender { get; set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                MessageOperation = (MessageOperation)reader.ReadUInt16();
                Sender = BigInteger.Parse(reader.ReadString());
                RandomID = BigInteger.Parse(reader.ReadString());

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
                writer.Write((UInt16)MessageOperation);
                writer.Write(Sender.ToString());
                writer.Write(RandomID.ToString());
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }
    }
}
