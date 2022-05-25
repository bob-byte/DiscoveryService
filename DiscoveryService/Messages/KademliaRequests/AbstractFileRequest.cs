using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Interfaces;

using System;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages.KademliaRequests
{
    internal abstract class AbstractFileRequest : Request
    {
        protected AbstractFileRequest( BigInteger senderKadId, String senderMachineId )
            : base( senderKadId, senderMachineId )
        {
            ;//do nothing
        }

        protected AbstractFileRequest( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;
        }

        public String FileOriginalName { get; set; }

        public String HexPrefix { get; set; }

        public String LocalBucketId { get; set; }

        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );

                writer.WriteUtf32String( FileOriginalName );
                writer.WriteAsciiString( HexPrefix );
                writer.WriteAsciiString( LocalBucketId );
            }
            else
            {
                throw new ArgumentNullException( $"{nameof( writer )} is null" );
            }
        }

        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );

                FileOriginalName = reader.ReadUtf32String();
                HexPrefix = reader.ReadAsciiString();
                LocalBucketId = reader.ReadAsciiString();
            }
            else
            {
                throw new ArgumentNullException( $"{nameof( reader )} is null" );
            }

            return this;
        }
    }
}
