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
                base.Read(reader);

                Sender = reader.ReadBigInteger();
                RandomID = reader.ReadBigInteger();

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

                writer.Write(Sender);
                writer.Write(RandomID);
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }
    }
}
