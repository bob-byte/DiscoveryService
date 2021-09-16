using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class StoreRequest : Request
    {
        public StoreRequest( BigInteger sender )
            : base( sender )
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

        public override String ToString()
        {
            using ( StringWriter writer = new StringWriter() )
            {
                writer.WriteLine( $"{GetType().Name}:\n" +
                                 $"{PropertyWithValue( nameof( RandomID ), RandomID )};\n" +
                                 $"{PropertyWithValue( nameof( Sender ), Sender )};\n" +
                                 $"{PropertyWithValue( nameof( KeyToStore ), KeyToStore )};\n" +
                                 $"{PropertyWithValue( nameof( Value ), Value )};\n" +
                                 $"{PropertyWithValue( nameof( IsCached ), IsCached )};\n" +
                                 $"{PropertyWithValue( nameof( ExpirationTimeSec ), ExpirationTimeSec )}" );

                return writer.ToString();
            }
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.Store;
    }
}
