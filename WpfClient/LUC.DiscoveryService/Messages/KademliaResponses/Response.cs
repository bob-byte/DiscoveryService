using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Interfaces;

using System;
using System.Net.Sockets;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    abstract class Response : Message
    {
        /// <summary>
        /// Use this constructor when you want to read remote response
        /// </summary>
        protected Response( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        /// <summary>
        /// Use this constructor when you want to send response
        /// </summary>
        protected Response( BigInteger requestRandomId )
        {
            RandomID = requestRandomId;
        }

        protected Response()
        {
            ;
        }

        public BigInteger RandomID { get; private set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if ( reader != null )
            {
                base.Read(reader);
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
            if ( writer != null )
            {
                base.Write(writer);
                writer.Write(RandomID);
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }
    }
}
