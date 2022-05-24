using System;
using System.Diagnostics;
using System.Threading;

namespace LUC.Interfaces.Models
{
    [DebuggerDisplay( "LocalFilePath = {LocalFilePath}" )]
    public sealed class DownloadingFileInfo: IDisposable
    {
        private CancellationToken m_cancellationToken;

        public DownloadingFileInfo()
        {
            SourceToCancelDownload = new CancellationTokenSource();
            m_cancellationToken = SourceToCancelDownload.Token;
        }

        public String Md5 { get; set; }

        public String Version { get; set; }

        public Boolean IsDownloaded { get; set; }

        public String LocalFilePath { get; set; }

        public String PathWhereDownloadFileFirst { get; set; }
        
        public String FileHexPrefix { get; set; }

        public DateTime LastModifiedDateTimeUtc { get; set; }

        public String OriginalName { get; set; }

        public Int64 ByteCount { get; set; }

        public String BucketId { get; set; }

        public String ServerBucketName { get; set; }

        public String Guid { get; set; }

        /// <summary>
        /// Original name of file in UTF-8 encoding
        /// </summary>
        public String ObjectKey { get; set; }

        /// <value>
        /// Default it is equal to <seealso cref="SourceToCancelDownload.Token"/>
        /// </value>
        public CancellationToken CancellationToken
        {
            get => m_cancellationToken;
            set
            {
                m_cancellationToken = value;

                SourceToCancelDownload.Dispose();
                SourceToCancelDownload = CancellationTokenSource.CreateLinkedTokenSource( m_cancellationToken );
            }
        }

        public CancellationTokenSource SourceToCancelDownload { get; private set; }

        public override Int32 GetHashCode() =>
            LocalFilePath.GetHashCode();

        public override Boolean Equals( Object obj ) =>
            ( obj is DownloadingFileInfo fileInfo ) && fileInfo.LocalFilePath.Equals( LocalFilePath, StringComparison.OrdinalIgnoreCase );

        public void Dispose() =>
            SourceToCancelDownload?.Dispose();
    }
}
