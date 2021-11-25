using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages.KademliaRequests
{
    class StoreRequest : Request
    {
        public StoreRequest( BigInteger senderKadId, String senderMachineId )
            : base( senderKadId, senderMachineId )
        {
            DefaultInit();
        }

        public StoreRequest()
        {
            DefaultInit();
        }

        public BigInteger KeyToStore { get; set; }
        public String Value { get; set; }
        public Boolean IsCached { get; set; }
        public Int32 ExpirationTimeSec { get; set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );

                KeyToStore = reader.ReadBigInteger();
                Value = reader.ReadAsciiString();
                IsCached = reader.ReadBoolean();
                ExpirationTimeSec = (Int32)reader.ReadUInt32();

                return this;
            }
            else
            {
                throw new ArgumentNullException( "ReaderNullException" );
            }
        }

        /// <inheritdoc/>
        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );

                writer.Write( KeyToStore );
                writer.WriteAsciiString( Value );
                writer.Write( IsCached );
                writer.Write( (UInt32)ExpirationTimeSec );
            }
            else
            {
                throw new ArgumentNullException( "WriterNullException" );
            }
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.Store;
    }
}
