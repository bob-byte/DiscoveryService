using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Exceptions;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace LUC.DiscoveryService.Kademlia.Downloads
{
    /// <summary>
    /// Thread safe class for download files
    /// </summary>
    public partial class Download
    {
        public event EventHandler<Exception> DownloadErrorHappened;

        /// <summary>
        /// Event argument is full path to downloaded file
        /// </summary>
        public event EventHandler<FileDownloadedEventArgs> FileDownloaded;

        /// <summary>
        /// Event argument is full path to downloaded file
        /// </summary>
        public event EventHandler<String> FilePartiallyDownloaded;

        private const String MESS_IF_FILE_DOESNT_EXIST_IN_ANY_NODE = "This file doesn't exist in any node";

        private readonly Object m_lockWriteFile;
        private readonly DownloadedFile m_downloadedFile;

        private readonly DiscoveryService m_discoveryService;
        private readonly Contact m_ourContact;

        public Download( DiscoveryService discoveryService, IOBehavior ioBehavior )
        {
            m_downloadedFile = new DownloadedFile();
            m_lockWriteFile = new Object();

            m_discoveryService = discoveryService;
            m_ourContact = discoveryService.NetworkEventInvoker.OurContact;

            LoggingService = AbstractService.LoggingService;
            IOBehavior = ioBehavior;
        }

        public IOBehavior IOBehavior { get; }

        public ILoggingService LoggingService { get; set; }

        /// <summary>
        /// Downloads a file of any size
        /// </summary>
        /// <param name="localFolderPath">
        /// Full path to file (except file name) where you want to download it
        /// </param>
        /// <param name="localBucketName">
        /// Group ID, which current user and remote contacts belong to
        /// </param>
        /// <param name="hexPrefix">
        /// The name of the folder on the server in hexadecimal which will contain <paramref name="localOriginalName"/> 
        /// </param>
        /// <param name="localOriginalName">
        /// The name of file on the server to download
        /// </param>
        /// <param name="bytesCount">
        /// Count of bytes of the file which exists on the server
        /// </param>
        /// <param name="fileVersion">
        /// Version of the <paramref name="localOriginalName"/> file on the server
        /// </param>
        /// <param name="cancellationToken">
        /// Token to cancel download process
        /// </param>
        public async Task DownloadFileAsync( String localFolderPath, String localBucketName, String hexPrefix, String localOriginalName,
            Int64 bytesCount, String fileVersion, CancellationToken cancellationToken, IProgress<ChunkRange> downloadProgress = null )
        {
            if ( cancellationToken != default )
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            CheckInputParameters( 
                localFolderPath, 
                localBucketName, 
                localOriginalName, 
                bytesCount, 
                fileVersion,
                out Boolean isRightInputParameters,
                out String fullFileName
            );

            if ( isRightInputParameters )
            {
                IEnumerable<Contact> onlineContacts = ContactsInSameBucket(localBucketName);

                try
                {
                    if ( onlineContacts.Count() >= 1 )
                    {
                        DownloadFileRequest initialRequest = new DownloadFileRequest( m_ourContact.KadId.Value, m_ourContact.MachineId )
                        {
                            FullPathToFile = fullFileName,
                            FileOriginalName = localOriginalName,
                            BucketId = localBucketName,
                            ChunkRange = new ChunkRange { Start = 0, Total = (UInt64)bytesCount },
                            HexPrefix = hexPrefix,
                            FileVersion = fileVersion
                        };

                        if ( bytesCount <= Constants.MAX_CHUNK_SIZE )
                        {
                            await DownloadSmallFileAsync( onlineContacts, initialRequest, cancellationToken, downloadProgress ).ConfigureAwait( false );
                        }
                        else
                        {
                            IEnumerable<Contact> contactsWithFile = ContactsWithFile( onlineContacts, initialRequest, cancellationToken );

                            await DownloadBigFileAsync( contactsWithFile, initialRequest, cancellationToken, downloadProgress ).ConfigureAwait( false );
                        }

                        FileDownloaded?.Invoke( sender: this, new FileDownloadedEventArgs( fullFileName, fileVersion ) );
                    }
                }
                catch ( OperationCanceledException ex )
                {
                    HandleException( ex );
                }
                catch ( InvalidOperationException ex )
                {
                    HandleException( ex );
                }
                catch ( PathTooLongException ex )
                {
                    HandleException( ex );
                }
                catch ( ArgumentException ex )
                {
                    HandleException( ex );
                }
                catch ( NotEnoughDriveSpaceException ex )
                {
                    HandleException( ex );
                }
                catch ( FilePartiallyDownloadedException ex )
                {
                    LoggingService.LogError( ex.ToString() );

                    FilePartiallyDownloaded?.Invoke( sender: this, fullFileName );
                }
                catch ( AggregateException ex )
                {
                    HandleException( ex.Flatten() );
                }
            }
        }

        private void HandleException( Exception exception )
        {
            LoggingService.LogError( exception.ToString() );

            DownloadErrorHappened?.Invoke( sender: this, exception );
        }

        //TODO: optimize it
        private void CheckInputParameters( 
            String localFolderPath, 
            String bucketName, 
            String localOriginalName,
            Int64 bytesCount, 
            String fileVersion,
            out Boolean isRightInputParameters,
            out String fullFileFile )
        {
            isRightInputParameters = ( !String.IsNullOrWhiteSpace( bucketName ) ) && ( !String.IsNullOrWhiteSpace( fileVersion ) ) && ( bytesCount > 0 );
            fullFileFile = m_downloadedFile.FullFileName( localFolderPath, localOriginalName );

            if ( isRightInputParameters )
            {
                //file shouldn't exist before download
                isRightInputParameters = ( isRightInputParameters ) && !File.Exists( fullFileFile );
                if ( !isRightInputParameters )
                {
                    LoggingService.LogInfo( $"File {fullFileFile} already exists" );
                }
            }
        }

        private IEnumerable<Contact> ContactsInSameBucket(String serverBucketName) =>
            m_discoveryService.OnlineContacts().Where( c => c.SupportedBuckets().Any( b => b == serverBucketName ) );

        private ExecutionDataflowBlockOptions ParallelOptions(CancellationToken cancellationToken) =>
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Constants.MAX_THREADS,
                CancellationToken = cancellationToken,
                BoundedCapacity = DataflowBlockOptions.Unbounded,//try to delete this row
                MaxMessagesPerTask = 1,
                EnsureOrdered = false
            };
    }
}
