using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages.KademliaResponses;

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class DownloadFileRequest : AbstractFileRequest, ICloneable
    {
        public DownloadFileRequest( BigInteger senderKadId, String senderMachineId )
            : base( senderKadId, senderMachineId )
        {
            DefaultInit();
        }

        public DownloadFileRequest()
        {
            DefaultInit();
        }

        //It is internal to not show all bytes in log(see method Display.ObjectToString)
        internal ChunkRange ChunkRange { get; set; }

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

        /// <summary>
        /// Also it removes first chunk from Range.NumsUndownloadedChunk
        /// </summary>
        public async ValueTask<(DownloadFileResponse response, RpcError rpcError, Boolean isRightResponse)> ResultAsyncWithCountDownloadedBytesUpdate( Contact remoteContact,
            IOBehavior ioBehavior, UInt16 protocolVersion )
        {
            (DownloadFileResponse downloadResponse, RpcError error) = await ResultAsync<DownloadFileResponse>( remoteContact,
            ioBehavior, protocolVersion ).ConfigureAwait( continueOnCapturedContext: false );

            Boolean isRightResponse = IsRightDownloadFileResponse( downloadResponse, error, remoteContact );
            if ( isRightResponse )
            {
                CountDownloadedBytes += (UInt64)downloadResponse.Chunk.Length;

                //try remove downloaded chunk number
                if ( ChunkRange.NumsUndownloadedChunk.Count > 0 )
                {
                    ChunkRange.NumsUndownloadedChunk.RemoveAt( index: 0 );
                }
            }

            return (downloadResponse, error, isRightResponse);
        }

        /// <summary>
        /// Use it after downloaded all bytes per <see cref="Contact"/>
        /// </summary>
        public void Update()
        {
            ChunkRange.TotalPerContact -= CountDownloadedBytes;
            CountDownloadedBytes = 0;
        }

        public Object Clone()
        {
            DownloadFileRequest clone = (DownloadFileRequest)MemberwiseClone();
            clone.ChunkRange = (ChunkRange)ChunkRange.Clone();

            return clone;
        }

        public override String ToString()
        {
            String requestAsStrWithoutChunkRange = base.ToString();

            StringBuilder stringBuilder = new StringBuilder( requestAsStrWithoutChunkRange );

            String chunkRangeAsStr = Display.ObjectToString( ChunkRange, initialTabulation: Display.TABULATION );
            stringBuilder.AppendLine( chunkRangeAsStr );

            return stringBuilder.ToString();
        }

        protected override void DefaultInit( params Object[] args )
        {
            base.DefaultInit();

            MessageOperation = MessageOperation.DownloadFile;
            CountDownloadedBytes = 0;
        }

        private Boolean IsRightDownloadFileResponse( DownloadFileResponse response, RpcError rpcError, Contact remoteContact )
        {
            Boolean isReceivedRequiredRange = ( !rpcError.HasError ) && ( response != null ) && ( response.IsRightBucket ) &&
                ( response.FileExists ) && ( response.Chunk != null ) && ( (Int32)( ChunkRange.End - ChunkRange.Start ) == response.Chunk.Length - 1 );

            //file can be changed in remote contact during download process
            Boolean isTheSameFileInRemoteContact;
            if ( isReceivedRequiredRange )
            {
                isTheSameFileInRemoteContact = ( response.FileVersion == FileVersion );
            }
            else
            {
                String logRecord = Display.StringWithAttention( $"File already doesn't exist in {remoteContact.MachineId}" );
                AbstractService.LoggingService.LogInfo( logRecord );

                isTheSameFileInRemoteContact = false;
            }

            return ( isReceivedRequiredRange ) && ( isTheSameFileInRemoteContact );
        }
    }
}
