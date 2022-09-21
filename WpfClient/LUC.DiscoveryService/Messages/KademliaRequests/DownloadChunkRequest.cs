using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Interfaces;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages.KademliaRequests
{
    internal sealed class DownloadChunkRequest : AbstractFileRequest, ICloneable
    {
        public DownloadChunkRequest( BigInteger senderKadId, String senderMachineId )
            : base( senderKadId, senderMachineId )
        {
            DefaultInit();
        }

        public DownloadChunkRequest( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        //It is internal to not show all bytes in log(see method Display.ObjectToString)
        internal ChunkRange ChunkRange { get; set; }

        public UInt64 CountDownloadedBytes { get; set; }

        /// <summary>
        /// For debugging only
        /// </summary>
        public List<Int32> NumsUndownloadedChunk { get; set; }

        public String FileVersion { get; set; }

        internal String PathWhereDownloadFileFirst { get; set; }

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
                throw new ArgumentNullException( nameof( writer ) );
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
                throw new ArgumentNullException( nameof( reader ) );
            }

            return this;
        }

        /// <summary>
        /// Also it removes first chunk from Range.NumsUndownloadedChunk
        /// </summary>
        public async ValueTask<(DownloadChunkResponse response, RpcError rpcError, Boolean isRightResponse)> ResultAsyncWithCountDownloadedBytesUpdate( 
            IContact remoteContact,
            IoBehavior ioBehavior, 
            UInt16 protocolVersion, 
            CancellationToken cancellationToken = default )
        {
            (DownloadChunkResponse downloadResponse, RpcError error) = await ResultAsync<DownloadChunkResponse>( 
                remoteContact,
                ioBehavior, 
                protocolVersion, 
                cancellationToken 
            ).ConfigureAwait( continueOnCapturedContext: false );

            Boolean isRightResponse = IsRightDownloadFileResponse( downloadResponse, error, remoteContact );
            ChunkRange.IsDownloaded = isRightResponse;

            //to be available to get next response (NetworkEventInvoker
            //ignores messages with the same ID that are in RecentMessages.Interval)
            RandomID = KademliaId.Random().Value;

            if ( isRightResponse )
            {
                CountDownloadedBytes += (UInt64)downloadResponse.Chunk.Length;

                //try remove downloaded chunk number
                if ( NumsUndownloadedChunk.Count > 0 )
                {
                    NumsUndownloadedChunk.RemoveAt( index: 0 );
                }
            }

            return (downloadResponse, error, isRightResponse);
        }

        public Object Clone()
        {
            var clone = (DownloadChunkRequest)MemberwiseClone();

            if ( ChunkRange != null )
            {
                clone.ChunkRange = (ChunkRange)ChunkRange.Clone();
            }

            if ( NumsUndownloadedChunk?.Count > 0 )
            {
                //get copy
                clone.NumsUndownloadedChunk = NumsUndownloadedChunk.ToList();
            }

            return clone;
        }

        public override String ToString()
        {
            String requestAsStrWithoutChunkRange = base.ToString();

            var stringBuilder = new StringBuilder( requestAsStrWithoutChunkRange );

            String chunkRangeAsStr = Display.ToString( ChunkRange, initialTabulation: Display.TABULATION_AS_STR );
            stringBuilder.AppendLine( chunkRangeAsStr );

            return stringBuilder.ToString();
        }

        protected override void DefaultInit( params Object[] args )
        {
            base.DefaultInit();

            NumsUndownloadedChunk = new List<Int32>();
            MessageOperation = MessageOperation.DownloadChunk;
            CountDownloadedBytes = 0;
        }

        private Boolean IsRightDownloadFileResponse( DownloadChunkResponse response, RpcError rpcError, IContact remoteContact )
        {
            Boolean isReceivedRequiredRange = !rpcError.HasError && ( response != null ) && response.IsRightBucket &&
                 response.FileExists && ( response.Chunk != null ) && ( (Int32)( ChunkRange.End - ChunkRange.Start ) == response.Chunk.Length - 1 );

            //file can be changed in remote contact during download process
            Boolean isTheSameFileInRemoteContact;
            if ( isReceivedRequiredRange )
            {
                isTheSameFileInRemoteContact = response.FileVersion.Equals( FileVersion, StringComparison.Ordinal );
            }
            else
            {
                String logRecord = $"A chunk cannot be downloaded from contact with ID {remoteContact.MachineId}, because ";
                String remoteErrorMessage = String.IsNullOrWhiteSpace( rpcError.ErrorMessage ) ?
                    "it hasn't already had required file" :
                    rpcError.ErrorMessage;
                logRecord += remoteErrorMessage;
                logRecord = logRecord.WithAttention();

                DsLoggerSet.DefaultLogger.LogInfo( logRecord );

                isTheSameFileInRemoteContact = false;
            }

            return isReceivedRequiredRange && isTheSameFileInRemoteContact;
        }
    }
}
