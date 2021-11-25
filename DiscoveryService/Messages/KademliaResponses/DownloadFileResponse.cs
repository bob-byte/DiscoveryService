using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Interfaces;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages.KademliaRequests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class DownloadFileResponse : AbstactFileResponse
    {
        public DownloadFileResponse( BigInteger requestRandomId )
            : base( requestRandomId )
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
                writer.WriteBytes( Chunk );
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
            StringBuilder stringBuilder = new StringBuilder( base.ToString() );

            stringBuilder.Append( $"{Display.VariableWithValue( $"Length of the {nameof(Chunk)}", Chunk.Length )};\n" );

            return stringBuilder.ToString();
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.DownloadFileResponse;
    }
}
