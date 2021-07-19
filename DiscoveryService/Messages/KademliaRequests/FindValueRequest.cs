using LUC.DiscoveryService.CodingData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class FindValueRequest : Request
    {
        public BigInteger IdOfContact { get; set; }

        public FindValueRequest()
            : base()
        {
            ;//do nothing
        }

        public FindValueRequest(UInt32 tcpPort)
            : base(tcpPort)
        {
            ;//do nothing
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
                writer.Write(IdOfContact.ToString());
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }
    }
}
