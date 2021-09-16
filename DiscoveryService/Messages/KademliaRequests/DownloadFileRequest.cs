﻿using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages.KademliaResponses;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class DownloadFileRequest : AbstractFileRequest, ICloneable
    {
        public DownloadFileRequest( BigInteger sender )
            : base( sender )
        {
            DefaultInit();
        }

        public DownloadFileRequest()
        {
            DefaultInit();
        }

        public ChunkRange ChunkRange { get; set; }

        public UInt64 CountDownloadedBytes { get; set; }

        public String FileVersion { get; set; }

        public String FullPathToFile { get; set; }

        /// <summary>
        /// Whether <see cref="Contact"/> has downloaded all the bytes for which it is responsible
        /// </summary>
        public Boolean WasDownloadedAllBytes => ChunkRange.TotalPerContact - CountDownloadedBytes == 0;

        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );
                writer.Write( ChunkRange );
                writer.WriteAsciiString( FileVersion );
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

                ChunkRange = reader.ReadRange();
                FileVersion = reader.ReadAsciiString();
            }
            else
            {
                throw new ArgumentNullException( $"{nameof( reader )} is null" );
            }

            return this;
        }

        public override String ToString()
        {
            StringBuilder stringBuilder = new StringBuilder( base.ToString() );

            stringBuilder.Append( $"{Display.PropertyWithValue( nameof( ChunkRange ), ChunkRange )};\n" );
            stringBuilder.Append( $"{Display.PropertyWithValue( nameof( FileVersion ), FileVersion )};\n" );
            stringBuilder.Append( $"{Display.PropertyWithValue( nameof( CountDownloadedBytes ), CountDownloadedBytes )};\n" );

            stringBuilder.Append( $"{nameof( ChunkRange.NumsUndownloadedChunk )}: " );

            foreach ( Int32 numChunk in ChunkRange.NumsUndownloadedChunk )
            {
                stringBuilder.Append( $"{numChunk}," );
            }

            stringBuilder.Remove( startIndex: stringBuilder.Length - 1, length: 1 );
            stringBuilder.Append( "\n" );

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Also it removes first chunk from Range.NumsUndownloadedChunk
        /// </summary>
        public async Task<(DownloadFileResponse, RpcError)> ResultAsyncWithCountDownloadedBytesUpdate( Contact remoteContact,
            IOBehavior ioBehavior, UInt16 protocolVersion )
        {
            (DownloadFileResponse downloadResponse, RpcError rpcError) = await ResultAsync<DownloadFileResponse>( remoteContact,
            ioBehavior, protocolVersion ).ConfigureAwait( continueOnCapturedContext: false );

            if ( ( !rpcError.HasError ) &&
               ( downloadResponse?.Chunk?.Length > 0 ) &&
               ( downloadResponse.FileVersion == FileVersion ) )
            {
                CountDownloadedBytes += (UInt64)downloadResponse.Chunk.Length;

                //try remove downloaded chunk number
                if ( ChunkRange.NumsUndownloadedChunk.Count > 0 )
                {
                    ChunkRange.NumsUndownloadedChunk.RemoveAt( index: 0 );
                }
            }

            return (downloadResponse, rpcError);
        }

        public Object Clone()
        {
            DownloadFileRequest clone = (DownloadFileRequest)MemberwiseClone();
            clone.ChunkRange = (ChunkRange)ChunkRange.Clone();

            return clone;
        }

        protected override void DefaultInit( params Object[] args )
        {
            MessageOperation = MessageOperation.DownloadFile;
            CountDownloadedBytes = 0;
        }
    }
}
