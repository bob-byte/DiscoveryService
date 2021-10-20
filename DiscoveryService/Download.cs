using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Kademlia.Exceptions;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Services.Implementation;

using Microsoft.Win32.SafeHandles;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace LUC.DiscoveryService
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
        public event EventHandler<String> FileIsDownloaded;

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
        /// <param name="bucketName">
        /// Group ID, which current user and remote contacts belong to. The last ones send bytes of the file to download
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
        public async Task DownloadFileAsync( String localFolderPath, String bucketName, String hexPrefix, String localOriginalName,
            Int64 bytesCount, String fileVersion, CancellationToken cancellationToken )
        {
            if ( cancellationToken != default )
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            Boolean isRightParameters = IsRightInputParameters( 
                localFolderPath, 
                bucketName, 
                localOriginalName, 
                bytesCount, 
                fileVersion 
            );

            if ( isRightParameters )
            {
                //add constraint of count contacts(see at Buckets)
                List<Contact> onlineContacts = m_discoveryService.OnlineContacts();

                //Full file name is path to file, file name and extension
                String fullFileName = m_downloadedFile.FullFileName(
                    localFolderPath,
                    localOriginalName
                );

                try
                {
                    if ( onlineContacts.Count >= 1 )
                    {
                        DownloadFileRequest initialRequest = new DownloadFileRequest( sender: m_ourContact.KadId.Value )
                        {
                            FullPathToFile = fullFileName,
                            FileOriginalName = localOriginalName,
                            BucketId = bucketName,
                            ChunkRange = new ChunkRange { Start = 0, Total = (UInt64)bytesCount },
                            HexPrefix = hexPrefix,
                            FileVersion = fileVersion
                        };

                        IEnumerable<Contact> contactsWithFile = ContactsWithFile( onlineContacts, initialRequest, cancellationToken );
                        if ( bytesCount <= Constants.MAX_CHUNK_SIZE )
                        {
                            await DownloadSmallFileAsync( contactsWithFile, initialRequest, cancellationToken ).ConfigureAwait( false );
                        }
                        else
                        {
                            await DownloadBigFileAsync( contactsWithFile, initialRequest, cancellationToken ).ConfigureAwait( false );
                        }

                        FileIsDownloaded?.Invoke( sender: this, fullFileName );
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

        private void TryDeletePartsOfUndownloadedBigFile( String fullFileName )
        {
            m_downloadedFile.TryDeleteFile( fullFileName );

            String tempFullFileName = m_downloadedFile.TempFullFileName( fullFileName );
            m_downloadedFile.TryDeleteFile( tempFullFileName );
        }

        //TODO: optimize it
        private Boolean IsRightInputParameters( 
            String localFolderPath, 
            String bucketName, 
            String localOriginalName,
            Int64 bytesCount, 
            String fileVersion )
        {
            //check first 4 parameters for null is in system methods
            Boolean isRightInputParameters = ( !String.IsNullOrWhiteSpace( bucketName ) ) && ( String.IsNullOrWhiteSpace( fileVersion ) ) && ( bytesCount > 0 );
            if ( isRightInputParameters )
            {
                //file shouldn't exist before download
                String fullFileFile = m_downloadedFile.FullFileName( localFolderPath, localOriginalName );

                isRightInputParameters = !File.Exists( fullFileFile );
                if ( !isRightInputParameters )
                {
                    LoggingService.LogInfo( $"File {fullFileFile} already exists" );
                }
            }

            return isRightInputParameters;
        }

        private ExecutionDataflowBlockOptions ParallelOptions(CancellationToken cancellationToken) =>
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Constants.MAX_THREADS,
                CancellationToken = cancellationToken,
                BoundedCapacity = DataflowBlockOptions.Unbounded,
                MaxMessagesPerTask = 1,
                EnsureOrdered = false
            };
    }
}
