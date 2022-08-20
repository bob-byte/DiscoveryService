using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;

using Nito.AsyncEx;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public partial class DownloaderFromLocalNetwork
    {
        private async ValueTask DownloadSmallFileAsync(
            IEnumerable<IContact> onlineContacts,
            DownloadChunkRequest initialRequest,
            UInt64 bytesFileCount,
            CancellationToken cancellationToken,
            IProgress<FileDownloadProgressArgs> downloadProgress
        )
        {
            //we need to cancel requesting another contacts whether they have file when we have downloaded it
            var cancelSource = new CancellationTokenSource();
            IEnumerable<IContact> contactsWithFile = ContactsWithFile( onlineContacts, initialRequest, CONTACT_COUNT_WITH_FILE_CAPACITY, bytesFileCount, cancelSource.Token );

            var request = (DownloadChunkRequest)initialRequest.Clone();
            request.ChunkRange = new ChunkRange( start: 0, end: bytesFileCount - 1, total: bytesFileCount );
            request.NumsUndownloadedChunk.Add( item: 0 );

            Boolean isRightDownloaded = false;

            Int64 signedBytesFileCount = (Int64)bytesFileCount;
            VerifyAbilityToDownloadFile( request.PathWhereDownloadFileFirst, signedBytesFileCount, out String bestPlaceWhereDownloadFile );
            request.PathWhereDownloadFileFirst = bestPlaceWhereDownloadFile;

            using ( FileStream fileStream = FileExtensions.FileStreamForDownload( request.PathWhereDownloadFileFirst ) )
            {
                FileExtensions.SetAttributesToTempDownloadingFile( request.PathWhereDownloadFileFirst );

                fileStream.SetLength( signedBytesFileCount );

                foreach ( IContact contact in contactsWithFile )
                {
                    //here small chunk is full file, because this file has length less than Constants.MaxChunkSize
                    (isRightDownloaded, _) = await DownloadProcessSmallChunkAsync( contact, request, fileStream, IOBehavior, cancellationToken, downloadProgress ).ConfigureAwait( continueOnCapturedContext: false );

                    if ( isRightDownloaded )
                    {
                        cancelSource.Cancel();
                        cancelSource.Dispose();

                        break;
                    }
                }
            }

            if ( !isRightDownloaded )
            {
                String pathFromSyncFolder = m_downloadingFile.FileNameFromSyncFolder( m_currentUserProvider.RootFolderPath, request.PathWhereDownloadFileFirst );
                throw new InvalidOperationException( $"Cannot download small file {pathFromSyncFolder}. Contact count which have this file = {contactsWithFile.Count()}" );
            }
        }

        /// <summary>
        /// <paramref name="downloadFileRequest"/> should be absolutelly initialized outside this method
        /// </summary>
        /// <returns>
        /// First value returns whether <see cref="DownloadChunkRequest.CountDownloadedBytes"/> is writen in <paramref name="fileStream"/>. The second returns <paramref name="downloadFileRequest"/> with updated <see cref="DownloadChunkRequest.CountDownloadedBytes"/>, <paramref name="downloadFileRequest"/> will not be changed
        /// </returns>
        private async ValueTask<(Boolean isBytesWritenInStream, DownloadChunkRequest updatedRequest)> DownloadProcessSmallChunkAsync(
            IContact remoteContact,
            DownloadChunkRequest downloadFileRequest,
            FileStream fileStream,
            IoBehavior ioBehavior,
            CancellationToken cancellationToken,
            IProgress<FileDownloadProgressArgs> downloadProgress
        )
        {
            Boolean isWritenInFile = false;
            var lastRequest = (DownloadChunkRequest)downloadFileRequest.Clone();

            (DownloadChunkResponse response, _, Boolean isRightResponse) = await downloadFileRequest.ResultAsyncWithCountDownloadedBytesUpdate
                    ( remoteContact, ioBehavior, m_discoveryService.ProtocolVersion, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );

            cancellationToken.ThrowIfCancellationRequested();

            if ( isRightResponse )
            {
                //if it is small file, we won't need to use seek, because we need to write from 0 position
                lock ( m_lockWriteFile )
                {
                    if ( fileStream.Position != (Int64)lastRequest.ChunkRange.Start )
                    {
                        fileStream.Seek( offset: (Int64)lastRequest.ChunkRange.Start, SeekOrigin.Begin );
                    }

                    fileStream.Write( response.Chunk, offset: 0, response.Chunk.Length );
                    downloadProgress?.Report( new FileDownloadProgressArgs( (ChunkRange)lastRequest.ChunkRange.Clone(), fileStream.Name ) );
                }

                isWritenInFile = true;
            }

            return (isWritenInFile, lastRequest);
        }

        private Boolean IsLastChunkPerContact( UInt64 start, UInt64 end ) =>
            start >= end;

        private async Task DownloadBigFileAsync(
            IEnumerable<IContact> contactsWithFile,
            DownloadChunkRequest initialRequest,
            UInt64 bytesCount,
            CancellationToken cancellationToken,
            IProgress<FileDownloadProgressArgs> downloadProgress
        )
        {
            //create temp file in order to another contacts don't download it
            String pathWhereDownloadFile = m_downloadingFile.TempFullFileName( initialRequest.PathWhereDownloadFileFirst );

            Boolean isFileFullyDownloaded = false;
            List<DataOfDownloadBigFileBlock> contactsWithRequest = null;

            IList<IContact> contactsWithFileList = contactsWithFile.ToList();
            if ( contactsWithFileList.Count == 0 )
            {
                String targetDownloadedFullFileName = PathExtensions.TargetDownloadedFullFileName( initialRequest.PathWhereDownloadFileFirst, m_currentUserProvider.RootFolderPath );
                String pathFromSyncFolder = m_downloadingFile.FileNameFromSyncFolder( m_currentUserProvider.RootFolderPath, targetDownloadedFullFileName );

                throw new InvalidOperationException( message: $"File {pathFromSyncFolder} with {initialRequest.FileVersion} version doesn\'t exists in any node" );
            }
            else
            {
                String contactsWithFileAsStr = contactsWithFileList.ToString( showAllPropsOfItems: true, initialTabulation: String.Empty, nameOfEnumerable: $"contacts with file {initialRequest.FileOriginalName}" );
                DsLoggerSet.DefaultLogger.LogInfo( contactsWithFileAsStr );

                GetContactsAndRequestsToDownload( initialRequest, contactsWithFileList, bytesCount, cancellationToken, out contactsWithRequest );

                Boolean isDownloadedAnyChunk = false;

                Int64 bytesStreamCount = (Int64)bytesCount;

                VerifyAbilityToDownloadFile( pathWhereDownloadFile, bytesStreamCount, out String bestPlaceWhereDownloadFile );
                pathWhereDownloadFile = bestPlaceWhereDownloadFile;
                initialRequest.PathWhereDownloadFileFirst = bestPlaceWhereDownloadFile;

                Int64 countDownloadedBytes = 0;
                var timer = new Timer( OnDownloadBigFileTimerElapsed, new Tuple<List<DataOfDownloadBigFileBlock>, Int64>(contactsWithRequest, countDownloadedBytes), dueTime: m_periodToShowDownloadProgress, period: m_periodToShowDownloadProgress );

                try
                {
                    using ( FileStream fileStream = FileExtensions.FileStreamForDownload( pathWhereDownloadFile ) )
                    {
                        m_downloadingFile.SetTempFileAttributes( pathWhereDownloadFile, fileStream.SafeFileHandle );

                        //we need to set fileStream.Length only at start downloading file. 
                        if ( fileStream.Length != bytesStreamCount )
                        {
                            fileStream.SetLength( bytesStreamCount );
                        }

                        ExecutionDataflowBlockOptions parallelOptions = ParallelOptions( cancellationToken );

                        do
                        {
                            //Producer/consumer pattern:
                            //current thread is producer and  produce contact and request to
                            //download some parts of the file using method ActionBlock.Post. 
                            //Consumers send request, receive response and write accepted bytes in stream.
                            var downloadProcess = new ActionBlock<DataOfDownloadBigFileBlock>( async ( contactWithRequest ) =>
                             {
                                 DownloadChunkRequest updatedRequest;
                                 Boolean isDownloadedAnyChunkFromContact;
                                 try
                                 {
                                     (updatedRequest, isDownloadedAnyChunkFromContact) = await DownloadProcessBigFileAsync(
                                         contactWithRequest,
                                         fileStream,
                                         IoBehavior.Synchronous,//otherwise we will have too many tasks
                                         cancellationToken,
                                         downloadProgress
                                     ).ConfigureAwait( continueOnCapturedContext: false );

                                     contactWithRequest.Request = updatedRequest;
                                 }
                                 finally
                                 {
                                     //we can get an exception in DownloadProcessBigFileAsync, so isDownloadedAnyChunkFromContact can be unset
                                     if ( !isDownloadedAnyChunk && contactWithRequest.ChunkRanges.Any( c => c.IsDownloaded ) )
                                     {
                                         isDownloadedAnyChunk = true;
                                     }
                                 }
                             }, parallelOptions );

                            for ( Int32 numContact = 0, numAttemptToPostContact = 0, countAttempts = contactsWithRequest.Count * 2;
                                  numContact < contactsWithRequest.Count && numAttemptToPostContact < countAttempts;
                                  numAttemptToPostContact++ )
                            {
                                DataOfDownloadBigFileBlock contactWithRequest = contactsWithRequest[ numContact ];

                                Boolean isPostedInActionBlock = IOBehavior == IoBehavior.Asynchronous ? 
                                    await downloadProcess.SendAsync( contactWithRequest ).ConfigureAwait( false ) : 
                                    downloadProcess.Post( contactWithRequest );

                                if ( isPostedInActionBlock )
                                {
                                    numContact++;
                                }
                                else
                                {
                                    DsLoggerSet.DefaultLogger.LogFatal( message: $"Contact with request isn't posted in {downloadProcess.GetType().Name}" );

                                    if ( IOBehavior == IoBehavior.Asynchronous )
                                    {
                                        await Task.Delay( m_timeWaitRepostContactInActionBlock ).ConfigureAwait( false );
                                    }
                                    else
                                    {
                                        Thread.Sleep( m_timeWaitRepostContactInActionBlock );
                                    }
                                }
                            }

                            //Signals that we will not post more contactWithRequest. 
                            //downloadProcess.Completion will never be completed without this calling
                            downloadProcess.Complete();

                            //await completion of download all file
                            Task taskDownloadProcess = downloadProcess.Completion;
                            if ( IOBehavior == IoBehavior.Asynchronous )
                            {
                                await taskDownloadProcess.ConfigureAwait( false );
                            }
                            else
                            {
                                AsyncContext.Run( () => taskDownloadProcess );
                            }

                            Int64 downloadedBytesByEachContact = contactsWithRequest.Select( c => (Int64)c.Request.CountDownloadedBytes ).Sum();
                            countDownloadedBytes += downloadedBytesByEachContact;

                            isFileFullyDownloaded = countDownloadedBytes == bytesStreamCount;
                            if ( !isFileFullyDownloaded )
                            {
                                //get new contacts and requests considering the previous download
                                contactsWithRequest = ContactsWithRequestForNewDownloadAttempt( contactsWithRequest, pathWhereDownloadFile, isDownloadedAnyChunk, cancellationToken );
                            }
                        }
                        while ( !isFileFullyDownloaded );
                    }
                }
                catch ( IOException ex )
                {
                    ex.Data.Add( TEMP_FULL_FILE_NAME_KEY, pathWhereDownloadFile );
                    throw;
                }
                catch ( OperationCanceledException ex )
                {
                    ex.Data.Add( TEMP_FULL_FILE_NAME_KEY, pathWhereDownloadFile );
                    throw;
                }
                finally
                {
                    timer.Stop();
                    timer.Dispose();
                }
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch ( OperationCanceledException ex )
            {
                ex.Data.Add( TEMP_FULL_FILE_NAME_KEY, pathWhereDownloadFile );
                throw;
            }
        }

        private void OnDownloadBigFileTimerElapsed(Object timerState)
        {
            var convertedState = timerState as Tuple<List<DataOfDownloadBigFileBlock>, Int64>;

            DataOfDownloadBigFileBlock sampleData = convertedState.Item1[ 0 ];
            Int64 fullFileSize = (Int64)sampleData.ChunkRanges[ 0 ].Total;
            Int64 alreadyDownloadedBytes = convertedState.Item1.Select( c => (Int64)c.Request.CountDownloadedBytes ).Sum() + convertedState.Item2;

            Double percents = (Double)alreadyDownloadedBytes / fullFileSize * 100;

            DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"    {sampleData.Request.FileOriginalName}: downloading {percents.ToString( "0.00", CultureInfo.InvariantCulture )}%. Now is {DateTime.UtcNow.ToLongTimeString()}" );
        }

        private async Task<(DownloadChunkRequest updatedRequest, Boolean isDownloadedAnyChunk)> DownloadProcessBigFileAsync(
            DataOfDownloadBigFileBlock dataOfDownloadBigFileBlock,
            FileStream fileStream,
            IoBehavior ioBehavior,
            CancellationToken cancellationToken,
            IProgress<FileDownloadProgressArgs> downloadProgress
        )
        {
            DownloadChunkRequest updatedRequest;
            Boolean isDownloadedAnyChunk;
            dataOfDownloadBigFileBlock.Request.ChunkRange = dataOfDownloadBigFileBlock.ChunkRanges.Last();

            if ( dataOfDownloadBigFileBlock.Request.ChunkRange.TotalPerContact <= DsConstants.MAX_CHUNK_SIZE )
            {
                (isDownloadedAnyChunk, updatedRequest) = await DownloadProcessSmallChunkAsync( dataOfDownloadBigFileBlock.Contact, dataOfDownloadBigFileBlock.Request, fileStream, ioBehavior, cancellationToken, downloadProgress ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else
            {
                (updatedRequest, isDownloadedAnyChunk) = await DownloadBigTotalPerContactBytesAsync( dataOfDownloadBigFileBlock.Contact, dataOfDownloadBigFileBlock.Request, dataOfDownloadBigFileBlock.ChunkRanges, fileStream, ioBehavior, cancellationToken, downloadProgress ).ConfigureAwait( false );
            }

            cancellationToken.ThrowIfCancellationRequested();

            return (updatedRequest, isDownloadedAnyChunk);
        }

        private async ValueTask<(DownloadChunkRequest updatedRequest, Boolean isDownloadedAnyChunk)> DownloadBigTotalPerContactBytesAsync(
            IContact remoteContact,
            DownloadChunkRequest sampleRequest,
            List<ChunkRange> chunkRangesPerContact,
            Stream fileStream,
            IoBehavior ioBehavior,
            CancellationToken cancellationToken,
            IProgress<FileDownloadProgressArgs> downloadProgress
        )
        {
            DownloadChunkRequest lastRequest = sampleRequest;

            Boolean isRightResponse = false;
            Boolean isDownloadedAnyChunk = false;
            Boolean isFirstRequest = true;

            for ( Int32 numChunkPerContact = 0; ( numChunkPerContact < chunkRangesPerContact.Count ) && ( isRightResponse || isFirstRequest ); numChunkPerContact++ )
            {
                lastRequest.ChunkRange = chunkRangesPerContact[ numChunkPerContact ];

                DownloadChunkResponse response;
                (response, _, isRightResponse) = await lastRequest.ResultAsyncWithCountDownloadedBytesUpdate( remoteContact, ioBehavior, m_discoveryService.ProtocolVersion, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );

                cancellationToken.ThrowIfCancellationRequested();

                if ( isRightResponse )
                {
                    //we use lock, because method fileStream.Write can be invoked from wrong position 
                    //(in case method seek is called by another thread)
                    lock ( m_lockWriteFile )
                    {
                        if ( fileStream.Position != (Int64)lastRequest.ChunkRange.Start )
                        {
                            fileStream.Seek( offset: (Int64)lastRequest.ChunkRange.Start, SeekOrigin.Begin );
                        }

                        fileStream.Write( response.Chunk, offset: 0, response.Chunk.Length );

                        downloadProgress?.Report( new FileDownloadProgressArgs( (ChunkRange)lastRequest.ChunkRange.Clone(), lastRequest.PathWhereDownloadFileFirst ) );
                    }

                    isDownloadedAnyChunk = true;
                }

                isFirstRequest = false;
            }

            return (lastRequest, isDownloadedAnyChunk);
        }

        private void GetContactsAndRequestsToDownload(
            DownloadChunkRequest sampleRequest,
            IList<IContact> contactsWithFile,
            UInt64 bytesCount,
            CancellationToken cancellationToken,
            out List<DataOfDownloadBigFileBlock> contactsAndRequests
        )
        {
            contactsAndRequests = new List<DataOfDownloadBigFileBlock>();

            UInt32 maxChunkSize = DsConstants.MAX_CHUNK_SIZE;

            UInt64 сountUndistributedBytes = bytesCount;
            UInt64 totalBytes = bytesCount;
            UInt64 lastPartBytesOfContact = 0;

            UInt32 chunkCountPerContact = (UInt32)Math.Ceiling( (Double)сountUndistributedBytes / maxChunkSize / contactsWithFile.Count );
            UInt32 partBytesOfContact = chunkCountPerContact > 1 ? maxChunkSize * chunkCountPerContact : maxChunkSize;

            Int32 numChunk = 0;
            for ( UInt32 numContact = 0;
                numContact < contactsWithFile.Count && сountUndistributedBytes > 0;
                numContact++, сountUndistributedBytes -= partBytesOfContact )
            {
                cancellationToken.ThrowIfCancellationRequested();

                var request = (DownloadChunkRequest)sampleRequest.Clone();

                UInt64 start = lastPartBytesOfContact;//previous finallyEnd + 1

                if ( ( сountUndistributedBytes < maxChunkSize ) || ( numContact == contactsWithFile.Count - 1 ) )
                {
                    partBytesOfContact = (UInt32)сountUndistributedBytes;
                }
                else
                {
                    lastPartBytesOfContact += partBytesOfContact;
                }

                UInt64 finallyEnd = partBytesOfContact - 1 + start;

                GetChunksForContact(
                    start,
                    finallyEnd,
                    maxChunkSize,
                    totalBytes,
                    ref numChunk,
                    out List<Int32> numsUndownloadedChunk,
                    out List<ChunkRange> chunkRangesPerContact
                );

                request.NumsUndownloadedChunk = numsUndownloadedChunk;

                contactsAndRequests.Add( new DataOfDownloadBigFileBlock( contactsWithFile[ (Int32)numContact ], request, chunkRangesPerContact ) );
            }
        }

        private void GetChunksForContact(
            UInt64 start,
            UInt64 finallyEnd,
            UInt32 maxChunkSize,
            UInt64 total,
            ref Int32 lastNumChunk,
            out List<Int32> numsChunk,
            out List<ChunkRange> chunkRanges
        )
        {
            numsChunk = new List<Int32>();
            chunkRanges = new List<ChunkRange>();

            UInt64 totalPerContact = finallyEnd + 1 - start;

            for ( UInt64 end = finallyEnd + 1 - start > maxChunkSize ? start + maxChunkSize - 1 : finallyEnd;
                 !IsLastChunkPerContact( start, end );
                 lastNumChunk++, start = end + 1, end = ( ( end + maxChunkSize ) <= finallyEnd ) ? ( end + maxChunkSize ) : finallyEnd )
            {
                numsChunk.Add( lastNumChunk );
                chunkRanges.Add( new ChunkRange( start, end, totalPerContact, total ) );
            }
        }

        private Boolean IsFileFullyDownloaded( String fullPathToFile, UInt64 countOfBytes, UInt64 downloadedBytesByEachContact )
        {
            Boolean isRightDownloaded = ( countOfBytes == downloadedBytesByEachContact ) && File.Exists( fullPathToFile );

            return isRightDownloaded;
        }

        /// <returns>
        /// <see cref="IContact"/>s with <see cref="DownloadChunkRequest"/>s with <see cref="ChunkRange"/>s which aren't downloaded before
        /// </returns>
        private List<DataOfDownloadBigFileBlock> ContactsWithRequestForNewDownloadAttempt(
            List<DataOfDownloadBigFileBlock> contactsWithRequest,
            String tempFullFileName,
            Boolean isDownloadedAnyChunk,
            CancellationToken cancellationToken
        )
        {
            var oldRequests = contactsWithRequest.Select( c => c.Request ).ToList();

            var requestsWithUndownloadedBytes = new List<(DownloadChunkRequest request, List<ChunkRange> undownloadedChunkRanges)>();
            UInt64 сountUndistributedBytes = 0;
            var contactsWhichHaveFile = new List<IContact>();

            foreach ( DataOfDownloadBigFileBlock contactAndRequest in contactsWithRequest )
            {
                DownloadChunkRequest request = contactAndRequest.Request;

                UInt64 undownloadedByteCount = request.ChunkRange.TotalPerContact - request.CountDownloadedBytes;

                if ( undownloadedByteCount > 0 )
                {
                    сountUndistributedBytes += undownloadedByteCount;
                    var undownloadedChunkRanges = new List<ChunkRange>( contactAndRequest.ChunkRanges.Where( c => !c.IsDownloaded ) );

                    requestsWithUndownloadedBytes.Add( (request, undownloadedChunkRanges) );
                }
                else if ( undownloadedByteCount == 0 )
                {
                    contactsWhichHaveFile.Add( contactAndRequest.Contact );
                }
            }

            DownloadChunkRequest sampleRequest = oldRequests.First();

            if ( contactsWhichHaveFile.Count < requestsWithUndownloadedBytes.Count )
            {
                Int32 minContactCount = requestsWithUndownloadedBytes.Count - contactsWhichHaveFile.Count;

                IEnumerable<IContact> otherOnlineContacts = m_discoveryService.OnlineContacts().Except( contactsWhichHaveFile );
                IEnumerable<IContact> otherContactsInSameBucket = ContactsInSameBucket( otherOnlineContacts, sampleRequest.LocalBucketId );

                cancellationToken.ThrowIfCancellationRequested();

                var newContactsWithFile = ContactsWithFile(
                    otherContactsInSameBucket,
                    sampleRequest,
                    minContactCount,
                    sampleRequest.ChunkRange.Total,
                    cancellationToken
                ).ToList();

                contactsWhichHaveFile.AddRange( newContactsWithFile );
            }

            if ( contactsWhichHaveFile.Count > 0 )
            {
                var newContactsWithRequest = new List<DataOfDownloadBigFileBlock>();
                UInt64 newTotalPerContact;

                for ( Int32 numContact = 0, numRequest = 0;
                      ( numRequest < requestsWithUndownloadedBytes.Count ) && ( сountUndistributedBytes > 0 );
                      numRequest++, сountUndistributedBytes -= newTotalPerContact )
                {
                    DownloadChunkRequest request = requestsWithUndownloadedBytes[ numRequest ].request;
                    List<ChunkRange> undownloadedChunkRanges = requestsWithUndownloadedBytes[ numRequest ].undownloadedChunkRanges;

                    foreach ( ChunkRange range in undownloadedChunkRanges )
                    {
                        range.TotalPerContact -= request.CountDownloadedBytes;
                    }

                    newTotalPerContact = requestsWithUndownloadedBytes[ numRequest ].undownloadedChunkRanges[ 0 ].TotalPerContact;

                    //if we change TotalPerContact we also should update CountDownloadedBytes
                    //in order to don't have discrepancy(see calling IsFileFullyDownloaded in DownloadBigFileAsync)
                    request.CountDownloadedBytes = 0;

                    newContactsWithRequest.Add( new DataOfDownloadBigFileBlock( contactsWhichHaveFile[ numContact ], request, undownloadedChunkRanges ) );

                    if ( numContact < contactsWhichHaveFile.Count - 1 )
                    {
                        numContact++;
                    }
                }

                return newContactsWithRequest;
            }
            else if ( isDownloadedAnyChunk )
            {
                throw new FilePartiallyDownloadedException( requestsWithUndownloadedBytes.SelectMany( c => c.undownloadedChunkRanges ), tempFullFileName, MESS_IF_FILE_DOESNT_EXIST_IN_ANY_NODE );
            }
            else
            {
                String targetPath = PathExtensions.TargetDownloadedFullFileName( tempFullFileName, m_currentUserProvider.RootFolderPath );
                throw new InvalidOperationException( message: $"None has file {targetPath} with version {sampleRequest.FileVersion}" );
            }
        }

        /// <returns>
        /// Contacts which is the same bucket and answered last request
        /// </returns>
        private IEnumerable<IContact> ContactsForRetryDownload( IEnumerable<IContact> contacts, String localBucketName, Int32 minContactCount )
        {
            IEnumerable<IContact> contactsInSameBucket = ContactsInSameBucket( contacts, localBucketName );

            Dht dht = NetworkEventInvoker.DistributedHashTable( m_discoveryService.ProtocolVersion );
            IEnumerable<IContact> contactsForRetryDownload = contactsInSameBucket.Where( c =>
             {
                 Boolean shouldCommunicateInDownload = !dht.EvictionCount.ContainsKey( c.KadId.Value );
                 if ( !shouldCommunicateInDownload )
                 {
                     shouldCommunicateInDownload = dht.EvictionCount[ c.KadId.Value ] == 0;
                 }

                 return shouldCommunicateInDownload;
             } );

            if ( contactsForRetryDownload.Count() < minContactCount )
            {
                contactsForRetryDownload = contactsInSameBucket;
            }

            return contactsForRetryDownload;
        }
    }
}
