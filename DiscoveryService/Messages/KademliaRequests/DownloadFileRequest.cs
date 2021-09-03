using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages.KademliaResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class DownloadFileRequest : FileRequest, ICloneable
    {
        public DownloadFileRequest()
        {
            MessageOperation = MessageOperation.DownloadFile;
            CountDownloadedBytes = 0;
        }

        public Range Range { get; set; }

        public UInt64 CountDownloadedBytes { get; set; }

        public String FileVersion { get; set; }

        public String FullPathToFile { get; set; }

        /// <summary>
        /// Whether <see cref="Contact"/> has downloaded all the bytes for which it is responsible
        /// </summary>
        public Boolean WasDownloadedAllBytes => Range.TotalPerContact - CountDownloadedBytes == 0;

        public override void Write(WireWriter writer)
        {
            if (writer != null)
            {
                base.Write(writer);
                writer.Write(Range);
                writer.WriteAsciiString(FileVersion);
            }
            else
            {
                throw new ArgumentNullException($"{nameof(writer)} is null");
            }
        }

        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                base.Read(reader);

                Range = reader.ReadRange();
                FileVersion = reader.ReadAsciiString();
            }
            else
            {
                throw new ArgumentNullException($"{nameof(reader)} is null");
            }

            return this;
        }

        public async Task<(DownloadFileResponse, RpcError)> ResultAsyncWithCountDownloadedBytesUpdate(Contact remoteContact,
            IOBehavior ioBehavior, UInt16 protocolVersion)
        {
            (DownloadFileResponse downloadResponse, RpcError rpcError) = await ResultAsync<DownloadFileResponse>(remoteContact,
            ioBehavior, protocolVersion).ConfigureAwait(continueOnCapturedContext: false);

            if(downloadResponse?.Chunk?.Length > 0)
            {
                CountDownloadedBytes += (UInt32)downloadResponse.Chunk.Length;
            }

            return (downloadResponse, rpcError);
        }

        public Object Clone()
        {
            var clone = (DownloadFileRequest)MemberwiseClone();
            clone.Range = (Range)Range.Clone();

            return clone;
        }
    }
}
