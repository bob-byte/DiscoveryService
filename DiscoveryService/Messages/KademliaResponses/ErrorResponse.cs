using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Interfaces;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class ErrorResponse : Response
    {
        public ErrorResponse()
        {
            ;//do nothing
        }

        public ErrorResponse( BigInteger randomId, String errorMessage )
            : base( randomId )
        {
            DefaultInit();
            ErrorMessage = errorMessage;
        }

        public String ErrorMessage { get; set; }

        public override void Write( WireWriter writer )
        {
            if(writer != null)
            {
                base.Write( writer );
                writer.WriteAsciiString( ErrorMessage );
            }
        }

        public override IWireSerialiser Read( WireReader reader )
        {
            if(reader != null)
            {
                base.Read( reader );
                ErrorMessage = reader.ReadAsciiString();

                return this;
            }
            else
            {
                throw new ArgumentException( nameof( reader ) );
            }
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.LocalError;
    }
}
