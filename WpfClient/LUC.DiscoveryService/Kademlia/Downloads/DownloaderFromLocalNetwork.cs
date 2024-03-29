﻿using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
    public partial class DownloaderFromLocalNetwork
    {
        /// <summary>
        /// Raises when exception during download is thrown, except <seealso cref="FilePartiallyDownloadedException"/>
        /// </summary>
        public event EventHandler<DownloadErrorHappenedEventArgs> DownloadErrorHappened;

        /// <summary>
        /// Raises when any file is successfully downloaded
        /// </summary>
        public event EventHandler<FileDownloadedEventArgs> FileSuccessfullyDownloaded;

        /// <summary>
        /// Raises when download process was started, but exception is occured 
        /// </summary>
        public event EventHandler<FilePartiallyDownloadedEventArgs> FilePartiallyDownloaded;

        private const String MESS_IF_FILE_DOESNT_EXIST_IN_ANY_NODE = "This file doesn't exist in any node";

        /// <summary>
        /// Key in <seealso cref="Exception.Data"/> of temp full file name
        /// </summary>
        private const String TEMP_FULL_FILE_NAME_KEY = "tempFullFileName";

        private const Int32 CONTACT_COUNT_WITH_FILE_CAPACITY = 10;

        private readonly TimeSpan m_timeWaitRepostContactInActionBlock = TimeSpan.FromSeconds( value: 0.5 );

        private readonly TimeSpan m_periodToShowDownloadProgress = TimeSpan.FromSeconds( value: 10 );

        private readonly Object m_lockWriteFile;
        private readonly DownloadingFile m_downloadingFile;

        private readonly IDiscoveryService m_discoveryService;
        private readonly IContact m_ourContact;

        private readonly ICurrentUserProvider m_currentUserProvider;

        private Int64 m_minFreeDriveSpace;

        public DownloaderFromLocalNetwork( IDiscoveryService discoveryService, IoBehavior ioBehavior, Int64 minFreeDriveSpace = 100000000 )
        {
            m_downloadingFile = new DownloadingFile();
            m_lockWriteFile = new Object();

            m_discoveryService = discoveryService;
            m_ourContact = discoveryService.OurContact;

            m_currentUserProvider = m_discoveryService.CurrentUserProvider;

            IOBehavior = ioBehavior;

            m_minFreeDriveSpace = minFreeDriveSpace;
        }

        public IoBehavior IOBehavior { get; set; }

        public Int64 MinFreeDriveSpace
        {
            get => m_minFreeDriveSpace;
            set => Interlocked.Exchange( ref m_minFreeDriveSpace, value );
        }

        /// <summary>
        /// Downloads a file of any size. 
        /// </summary>
        /// <param name="localFolderPath">
        /// Full path to file (except file name) where you want to download it
        /// </param>
        /// <param name="bucketId">
        /// Group ID, which current user and remote contacts belong to
        /// </param>
        /// <param name="hexPrefix">
        /// The name of the folder on the server in hexadecimal which will contain <paramref name="localOriginalName"/> 
        /// </param>
        /// <param name="objectDescription">
        /// Description of file which should be downloaded
        /// </param>
        /// <param name="cancellationToken">
        /// Token to cancel download process
        /// </param>
        /// <param name="downloadedAnyChunk">
        /// It is set if any chunk is downloaded. Note that it isn't be set when first chunk was downloaded, 
        /// it is when all download process was finished
        /// </param>
        /// <param name="downloadProgress">
        /// It is reported when certain chunk is downloaded. Remember that chunks are downloaded in unsorted order
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="downloadingFileInfo"/> is null
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// If <paramref name="cancellationToken"/> is canceled 
        /// (this method checks before download process, during and after)
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// If any DS doesn't find any node or no one has <paramref name="localOriginalName"/> file 
        /// or cannot mark big file ( size more than <seealso cref="DsConstants.MAX_CHUNK_SIZE"/>) as sparse
        /// </exception>
        /// <exception cref="FilePartiallyDownloadedException">
        /// Download process was started, but received some exception(some chunk can be downloaded or not)
        /// </exception>
        /// <exception cref="IOException">
        /// <a href="https://docs.microsoft.com/th-TH/dotnet/api/system.io.driveinfo.isready?view=net-6.0">Disk  isn't ready</a>
        /// or little free disk space or writing file or cannot seek or write downloaded file or too long path was created for downloaded file
        /// </exception>
        public Task DownloadFileAsync(
            DownloadingFileInfo downloadingFileInfo,
            IFileChangesQueue fileChangesQueue = null,
            IProgress<FileDownloadProgressArgs> downloadProgress = null
        )
        {
            //Because of the way async/await methods are rewritten by the compiler, any exceptions
            //thrown during the parameters check will happen only when the task is observed.
            //It is the reason why is DownloadFileInternalAsync created
            if ( downloadingFileInfo != null )
            {
                Task downloadTask = DownloadFileInternalAsync( downloadingFileInfo, fileChangesQueue, downloadProgress );
                return downloadTask;
            }
            else
            {
                throw new ArgumentNullException( paramName: nameof( downloadingFileInfo ) );
            }
        }

        public void TryGetTempFullFileNameFromException( Exception exception, out String tempFullFileName, out Boolean isInExceptionData )
        {
            isInExceptionData = exception.Data.Contains( TEMP_FULL_FILE_NAME_KEY );
            tempFullFileName = isInExceptionData ? (String)exception.Data[ TEMP_FULL_FILE_NAME_KEY ] : null;
        }

        public void VerifyAbilityToDownloadFile( String fullFileName, Int64 bytesCountOfFile, out String bestPlaceWhereDownloadFile )
        {
            String driveName = fullFileName[ 0 ].ToString();
            var driveInfo = new DriveInfo( driveName );

            Int64 freeDiskSpaceAfterDownload = driveInfo.AvailableFreeSpace - ( bytesCountOfFile + MinFreeDriveSpace );//free disk space after download process, except MinFreeDriveSpace

            Boolean isEnoughFreeDiskSpace = freeDiskSpaceAfterDownload >= 0;
            if ( isEnoughFreeDiskSpace )
            {
                bestPlaceWhereDownloadFile = fullFileName;
            }
            else
            {
                bestPlaceWhereDownloadFile = PathExtensions.TargetDownloadedFullFileName( fullFileName, m_currentUserProvider.RootFolderPath );
                var fileInfo = new FileInfo( bestPlaceWhereDownloadFile );
                if ( fileInfo.Exists )
                {
                    freeDiskSpaceAfterDownload = driveInfo.AvailableFreeSpace - ( bytesCountOfFile - fileInfo.Length + MinFreeDriveSpace );
                    isEnoughFreeDiskSpace = freeDiskSpaceAfterDownload >= 0;
                }

                if ( !isEnoughFreeDiskSpace )
                {
                    String pathAfterSyncFolder = m_downloadingFile.FileNameFromSyncFolder( m_currentUserProvider.RootFolderPath, fullFileName );
                    throw new NotEnoughDriveSpaceException( fullFileName, message: $"You need to free {-freeDiskSpaceAfterDownload} to download {pathAfterSyncFolder}" );
                }
            }
        }

        private async Task DownloadFileInternalAsync(
            DownloadingFileInfo downloadingFileInfo,
            IFileChangesQueue fileChangesQueue = null,
            IProgress<FileDownloadProgressArgs> downloadProgress = null
        )
        {
            List<IContact> onlineContacts = m_discoveryService.OnlineContacts();
            var contactsInSameBucket = ContactsInSameBucket( onlineContacts, downloadingFileInfo.BucketId ).ToList();

            try
            {
                if ( contactsInSameBucket.Count >= 1 )
                {
                    var initialRequest = new DownloadChunkRequest( m_ourContact.KadId.Value, m_ourContact.MachineId )
                    {
                        PathWhereDownloadFileFirst = downloadingFileInfo.PathWhereDownloadFileFirst,
                        FileOriginalName = downloadingFileInfo.OriginalName,
                        LocalBucketId = downloadingFileInfo.BucketId,
                        HexPrefix = downloadingFileInfo.FileHexPrefix,
                        FileVersion = downloadingFileInfo.Version
                    };

                    UInt64 totalFileBytesCount = (UInt64)downloadingFileInfo.ByteCount;

                    if ( totalFileBytesCount <= DsConstants.MAX_CHUNK_SIZE )
                    {
                        await DownloadSmallFileAsync(
                            contactsInSameBucket,
                            initialRequest,
                            totalFileBytesCount,
                            downloadingFileInfo.CancellationToken,
                            downloadProgress
                        ).ConfigureAwait( continueOnCapturedContext: false );
                    }
                    else
                    {
                        IEnumerable<IContact> contactsWithFile = ContactsWithFile(
                            contactsInSameBucket,
                            initialRequest,
                            totalFileBytesCount,
                            undownloadedfileBytesCount: downloadingFileInfo.ByteCount,
                            downloadingFileInfo.CancellationToken
                        );

                        await DownloadBigFileAsync(
                            contactsWithFile,
                            initialRequest,
                            totalFileBytesCount,
                            downloadingFileInfo.CancellationToken,
                            downloadProgress
                        ).ConfigureAwait( false );
                    }

                    FileExtensions.SetDownloadedFileToNormal( downloadingFileInfo, fileChangesQueue );

                    FileSuccessfullyDownloaded?.Invoke( sender: this, new FileDownloadedEventArgs( downloadingFileInfo.LocalFilePath, downloadingFileInfo.Version, downloadingFileInfo.Guid ) );
                }
                else
                {
                    String pathFromSyncFolder = m_downloadingFile.FileNameFromSyncFolder( m_currentUserProvider.RootFolderPath, downloadingFileInfo.LocalFilePath );
                    throw new InvalidOperationException( $"{nameof( DiscoveryService )} didn\'t find any node in the same bucket to download {pathFromSyncFolder}" );
                }
            }
            catch ( OperationCanceledException ex )
            {
                downloadingFileInfo.LocalFilePath = FullFileNameAfterDownloadBigFileException( ex, downloadingFileInfo.LocalFilePath );

                HandleException( ex, downloadingFileInfo.LocalFilePath );
                throw;
            }
            catch ( InvalidOperationException ex )
            {
                HandleException( ex, downloadingFileInfo.LocalFilePath );
                throw;
            }
            catch ( IOException ex )
            {
                downloadingFileInfo.LocalFilePath = FullFileNameAfterDownloadBigFileException( ex, downloadingFileInfo.LocalFilePath );

                HandleException( ex, downloadingFileInfo.LocalFilePath );
                throw;
            }
            catch ( FilePartiallyDownloadedException ex )
            {
                DsLoggerSet.DefaultLogger.LogFatal( ex.ToString() );

                FilePartiallyDownloaded?.Invoke( sender: this, new FilePartiallyDownloadedEventArgs( ex.UndownloadedRanges ) );
                throw;
            }
        }

        private void HandleException( Exception exception, String fullFileName ) => 
            DownloadErrorHappened?.Invoke( sender: this, new DownloadErrorHappenedEventArgs( exception, fullFileName ) );

        private IEnumerable<IContact> ContactsInSameBucket( IEnumerable<IContact> contacts, String bucketId ) =>
            contacts.Where( c => c.Buckets().Any( b => b.Equals( bucketId, StringComparison.OrdinalIgnoreCase ) ) );

        private String FullFileNameAfterDownloadBigFileException( Exception exception, String originalFullFileName )
        {
            String fullFileName = (String)originalFullFileName.Clone();

            TryGetTempFullFileNameFromException( exception, out String tempFullFileName, out Boolean isInExceptionData );

            if ( isInExceptionData )
            {
                fullFileName = tempFullFileName;
            }

            return fullFileName;
        }

        private ExecutionDataflowBlockOptions ParallelOptions( CancellationToken cancellationToken ) =>
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DsConstants.MAX_THREADS,
                CancellationToken = cancellationToken,
                BoundedCapacity = DataflowBlockOptions.Unbounded,//set explicitly available data which can be posted
                MaxMessagesPerTask = 1,
                EnsureOrdered = true //in order to another contacts can read our downloaded chunks
            };
    }
}
