using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    /// <summary>
    /// Thread safe class for download files
    /// </summary>
    public partial class Download
    {
        /// <summary>
        /// Raises when exception is thrown
        /// </summary>
        public event EventHandler<DownloadErrorHappenedEventArgs> DownloadErrorHappened;

        /// <summary>
        /// Raises when any file is successfully downloaded
        /// </summary>
        public event EventHandler<FileDownloadedEventArgs> FileSuccessfullyDownloaded;

        /// <summary>
        /// Raises when download process was started, but exception is occured 
        /// (this does not indicate that any chunks have been downloaded)
        /// </summary>
        public event EventHandler<FilePartiallyDownloadedEventArgs> FilePartiallyDownloaded;

        private const String MESS_IF_FILE_DOESNT_EXIST_IN_ANY_NODE = "This file doesn't exist in any node";
        private const Int32 CONTACT_COUNT_WITH_FILE_CAPACITY = 10;

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
        /// Downloads a file of any size. 
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
        /// <exception cref="ArgumentException">
        /// Any string parameter is null or whitespace or cannot using it check file exists or <paramref name="bytesCount"/> isn't more then 0
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// If <paramref name="cancellationToken"/> is canceled (it checks before download process, during and after) 
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// If any DS doesn't find any node or no one has <paramref name="localOriginalName"/> file
        /// </exception>
        /// <exception cref="FilePartiallyDownloadedException">
        /// Download process was started, but received some exception(some chunk can be downloaded or not)
        /// </exception>
        /// <exception cref="PathTooLongException">
        /// Too long path was created for downloaded file
        /// </exception>
        /// <exception cref="NotEnoughDriveSpaceException">
        /// Too little free disk space
        /// </exception>
        /// <exception cref="IOException">
        /// <a href="https://docs.microsoft.com/th-TH/dotnet/api/system.io.driveinfo.isready?view=net-6.0">Disk  isn't ready</a> for writing file
        /// </exception>
        public async Task DownloadFileAsync( String localFolderPath, String localBucketName, String hexPrefix, String localOriginalName,
            Int64 bytesCount, String fileVersion, CancellationToken cancellationToken, IProgress<FileDownloadProgressArgs> downloadProgress = null )
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
                List<Contact> onlineContacts = m_discoveryService.OnlineContacts();
                List<Contact> contactsInSameBucket = ContactsInSameBucket( onlineContacts, localBucketName ).ToList();

                try
                {
                    if ( contactsInSameBucket.Count >= 1 )
                    {
                        DownloadFileRequest initialRequest = new DownloadFileRequest( m_ourContact.KadId.Value, m_ourContact.MachineId )
                        {
                            FullFileName = fullFileName,
                            FileOriginalName = localOriginalName,
                            LocalBucketId = localBucketName,
                            ChunkRange = new ChunkRange { Start = 0, Total = (UInt64)bytesCount },
                            HexPrefix = hexPrefix,
                            FileVersion = fileVersion
                        };

                        if ( bytesCount <= Constants.MAX_CHUNK_SIZE )
                        {
                            await DownloadSmallFileAsync( contactsInSameBucket, initialRequest, cancellationToken, downloadProgress ).ConfigureAwait( continueOnCapturedContext: false );
                        }
                        else
                        {
                            IEnumerable<Contact> contactsWithFile = ContactsWithFile( contactsInSameBucket, initialRequest, cancellationToken, bytesCount );

                            await DownloadBigFileAsync( contactsWithFile, initialRequest, cancellationToken, downloadProgress ).ConfigureAwait( false );
                        }

                        FileSuccessfullyDownloaded?.Invoke( sender: this, new FileDownloadedEventArgs( fullFileName, fileVersion ) );
                    }
                    else
                    {
                        throw new InvalidOperationException( $"{nameof( DiscoveryService )} didn\'t find any node in the same bucket" );
                    }
                }
                catch ( OperationCanceledException ex )
                {
                    HandleException( ex, fullFileName );
                }
                catch ( InvalidOperationException ex )
                {
                    HandleException( ex, fullFileName );
                }
                catch ( PathTooLongException ex )
                {
                    HandleException( ex, fullFileName );
                }
                catch ( NotEnoughDriveSpaceException ex )
                {
                    HandleException( ex, fullFileName );
                }
                catch ( IOException ex )
                {
                    HandleException( ex, fullFileName );
                }
                catch ( FilePartiallyDownloadedException ex )
                {
                    LoggingService.LogError( ex.ToString() );

                    FilePartiallyDownloaded?.Invoke( sender: this, new FilePartiallyDownloadedEventArgs( ex.Ranges ) );
                    throw;
                }
            }
        }

        private void HandleException( Exception exception, String originalFullFileName )
        {
            LoggingService.LogError( exception.ToString() );

            DownloadErrorHappened?.Invoke( sender: this, new DownloadErrorHappenedEventArgs( exception, originalFullFileName ) );
            throw exception;
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

            if ( isRightInputParameters )
            {
                fullFileFile = m_downloadedFile.FullFileName( localFolderPath, localOriginalName );

                //file shouldn't exist before download
                isRightInputParameters = isRightInputParameters && !File.Exists( fullFileFile );
                if ( !isRightInputParameters )
                {
                    String record = $"File {fullFileFile} already exists";

                    LoggingService.LogInfo( record );
                    throw new ArgumentException( record );
                }
            }
            else
            {
                throw new ArgumentException();
            }
        }

        private IEnumerable<Contact> ContactsInSameBucket( IEnumerable<Contact> contacts, String serverBucketName ) =>
            contacts.Where( c => c.Buckets().Any( b => b.Equals( serverBucketName, StringComparison.OrdinalIgnoreCase ) ) );

        private ExecutionDataflowBlockOptions ParallelOptions(CancellationToken cancellationToken) =>
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Constants.MAX_THREADS,
                CancellationToken = cancellationToken,
                BoundedCapacity = DataflowBlockOptions.Unbounded,//set explicitly available data which can be posted
                MaxMessagesPerTask = 1,
                EnsureOrdered = false
            };
    }
}
