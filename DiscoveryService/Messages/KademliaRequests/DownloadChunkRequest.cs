using DiscoveryServices.CodingData;
using DiscoveryServices.Common;
using DiscoveryServices.Interfaces;
using DiscoveryServices.Kademlia;
using DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DiscoveryServices.Messages.KademliaRequests
{
    internal sealed class DownloadChunkRequest : AbstractFileRequest, ICloneable
    {
        public DownloadChunkRequest(BigInteger senderKadId, String senderMachineId)
            : base(senderKadId, senderMachineId)
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

        public String PathWhereDownloadFileFirst { get; set; }

        /// <summary>
        /// Whether <see cref="IContact"/> has downloaded all the bytes for which it is responsible
        /// </summary>
        public Boolean WasDownloadedAllBytes => ChunkRange.TotalPerContact - CountDownloadedBytes == 0;

        public override void Write(WireWriter writer)
        {
            if ( writer != null )
            {
                base.Write(writer);
                writer.Write(ChunkRange);
                writer.WriteAsciiString(FileVersion);
            }
            else
            {
                throw new ArgumentNullException($"{nameof(writer)} is null");
            }
        }

        public override IWireSerialiser Read(WireReader reader)
        {
            if ( reader != null )
            {
                base.Read(reader);

                ChunkRange = reader.ReadRange();
                FileVersion = reader.ReadAsciiString();
            }
            else
            {
                throw new ArgumentNullException($"{nameof(reader)} is null");
            }

            return this;
        }

        /// <summary>
        /// Also it removes first chunk from Range.NumsUndownloadedChunk
        /// </summary>
        public async ValueTask<(DownloadChunkResponse response, RpcError rpcError, Boolean isRightResponse)> ResultAsyncWithCountDownloadedBytesUpdate(IContact remoteContact,
            IoBehavior ioBehavior, UInt16 protocolVersion)
        {
            (DownloadChunkResponse downloadResponse, RpcError error) = await ResultAsync<DownloadChunkResponse>(remoteContact,
            ioBehavior, protocolVersion).ConfigureAwait(continueOnCapturedContext: false);

            Boolean isRightResponse = IsRightDownloadFileResponse(downloadResponse, error, remoteContact);
            ChunkRange.IsDownloaded = isRightResponse;

            if ( isRightResponse )
            {
                CountDownloadedBytes += (UInt64)downloadResponse.Chunk.Length;

                //try remove downloaded chunk number
                if ( NumsUndownloadedChunk.Count > 0 )
                {
                    NumsUndownloadedChunk.RemoveAt(index: 0);
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

            var stringBuilder = new StringBuilder(requestAsStrWithoutChunkRange);

            String chunkRangeAsStr = Display.ToString(ChunkRange, initialTabulation: Display.TABULATION);
            stringBuilder.AppendLine(chunkRangeAsStr);

            return stringBuilder.ToString();
        }

        protected override void DefaultInit(params Object[] args)
        {
            base.DefaultInit();

            NumsUndownloadedChunk = new List<Int32>();
            MessageOperation = MessageOperation.DownloadChunk;
            CountDownloadedBytes = 0;
        }

        private Boolean IsRightDownloadFileResponse(DownloadChunkResponse response, RpcError rpcError, IContact remoteContact)
        {
            Boolean isReceivedRequiredRange = !rpcError.HasError && (response != null) && response.IsRightBucket &&
                 response.FileExists && (response.Chunk != null) && ((Int32)(ChunkRange.End - ChunkRange.Start) == response.Chunk.Length - 1);

            //file can be changed in remote contact during download process
            Boolean isTheSameFileInRemoteContact;
            if ( isReceivedRequiredRange )
            {
                isTheSameFileInRemoteContact = response.FileVersion.Equals(FileVersion, StringComparison.Ordinal);
            }
            else
            {
                String logRecord = $"File already doesn't exist in {remoteContact.MachineId}".WithAttention();
                DsLoggerSet.DefaultLogger.LogInfo( logRecord );

                isTheSameFileInRemoteContact = false;
            }

            return isReceivedRequiredRange && isTheSameFileInRemoteContact;
        }
    }
}
