using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Interfaces;

using System;
using System.Numerics;
using System.Text;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class DownloadChunkResponse : AbstractFileResponse
    {
        public DownloadChunkResponse( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        public DownloadChunkResponse( BigInteger requestRandomId )
            : base( requestRandomId )
        {
            DefaultInit();
        }

        //To use map from CheckFileExistsResponse
        public DownloadChunkResponse()
        {
            DefaultInit();
        }

        //It is internal to not show all bytes in log(see method Display.ObjectToString)
        internal Byte[] Chunk { get; set; }

        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );

                writer.Write( (UInt32)Chunk.Length );
                if ( Chunk.Length > 0 )
                {
                    writer.WriteBytes( Chunk );
                }
            }
        }

        public override IWireSerialiser Read( WireReader reader )
        {
            base.Read( reader );

            UInt32 bytesCount = reader.ReadUInt32();
            Chunk = reader.ReadBytes( (Int32)bytesCount );

            return this;
        }

        public override String ToString()
        {
            var stringBuilder = new StringBuilder( base.ToString() );

            stringBuilder.Append( $"{Display.VariableWithValue( $"Length of the {nameof( Chunk )}", Chunk.Length )};\n" );

            return stringBuilder.ToString();
        }

        protected override void DefaultInit( params Object[] args ) =>
            MessageOperation = MessageOperation.DownloadChunkResponse;
    }
}
