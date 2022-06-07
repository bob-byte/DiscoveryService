using System;
using System.Numerics;

using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common.Interfaces;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class ErrorResponse : Response
    {
        public ErrorResponse( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        public ErrorResponse(BigInteger randomId, String errorMessage)
            : base(randomId)
        {
            DefaultInit();
            ErrorMessage = errorMessage;
        }

        public String ErrorMessage { get; set; }

        public override void Write(WireWriter writer)
        {
            if ( writer != null )
            {
                base.Write(writer);
                writer.WriteUtf32String(ErrorMessage);
            }
            else
            {
                throw new ArgumentNullException(nameof(writer));
            }
        }

        public override IWireSerialiser Read(WireReader reader)
        {
            if ( reader != null )
            {
                base.Read(reader);
                ErrorMessage = reader.ReadUtf32String();

                return this;
            }
            else
            {
                throw new ArgumentException(nameof(reader));
            }
        }

        protected override void DefaultInit(params Object[] args) =>
            MessageOperation = MessageOperation.LocalError;
    }
}
