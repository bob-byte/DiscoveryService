using LUC.DiscoveryService.CodingData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    public abstract class Response : Message
    {
        public BigInteger RandomID { get; set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
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
                writer.Write(RandomID.ToString());
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }
    }
}
