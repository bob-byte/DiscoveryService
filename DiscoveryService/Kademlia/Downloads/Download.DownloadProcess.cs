using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Exceptions;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace LUC.DiscoveryService.Kademlia.Downloads
{
    public partial class Download
    {
        private const Int32 MIN_CONTACT_FOR_RETRY_DOWNLOAD = 2;

        private async Task DownloadSmallFileAsync( IEnumerable<Contact> onlineContacts, DownloadFileRequest initialRequest, CancellationToken cancellationToken, IProgress<ChunkRange> downloadProgress )
        {
            //we need to cancel requesting another contacts whether they have file when we have downloaded it
            CancellationTokenSource cancelSource = new CancellationTokenSource();
            IEnumerable<Contact> contactsWithFile = ContactsWithFile( onlineContacts, initialRequest, cancelSource.Token );

            DownloadFileRequest request = (DownloadFileRequest)initialRequest.Clone();
            request.ChunkRange.End = request.ChunkRange.Total - 1;
            request.ChunkRange.NumsUndownloadedChunk.Add( 0 );

            Boolean isRightDownloaded = false;

            using ( Stream fileStream = File.OpenWrite( request.FullPathToFile ) )
            {
                foreach ( Contact contact in contactsWithFile )
                {
                    //here small chunk is full file, because this file has length less than Constants.MaxChunkSize
                    (isRightDownloaded, _) = await DownloadProcessSmallChunkAsync( contact, request, fileStream, cancellationToken, downloadProgress ).ConfigureAwait( continueOnCapturedContext: false );

                    if ( isRightDownloaded )
                    {
                        cancelSource.Cancel();
                        break;
                    }
                }
            }

            if ( !isRightDownloaded )
            {
                m_downloadedFile.TryDeleteFile( request.FullPathToFile );
                throw new InvalidOperationException( $"Cannot download small file {request.FullPathToFile}. Contact count which have this file = {contactsWithFile.Count()}" );
            }
        }

        /// <summary>
        /// <paramref name="downloadFileRequest"/> should be absolutelly initialized outside this method
        /// </summary>
        /// <returns>
        /// First value returns whether <see cref="DownloadFileRequest.CountDownloadedBytes"/> is writen in <paramref name="fileStream"/>. The second returns <paramref name="downloadFileRequest"/> with updated <see cref="DownloadFileRequest.CountDownloadedBytes"/>, <paramref name="downloadFileRequest"/> will not be changed
        /// </returns>
        private async Task<(Boolean, DownloadFileRequest)> DownloadProcessSmallChunkAsync( Contact remoteContact,
            DownloadFileRequest downloadFileRequest, Stream fileStream, CancellationToken cancellationToken, IProgress<ChunkRange> downloadProgress )
        {
            Boolean isWritenInFile = false;
            DownloadFileRequest lastRequest = (DownloadFileRequest)downloadFileRequest.Clone();

            (DownloadFileResponse response, RpcError rpcError) = await lastRequest.ResultAsyncWithCountDownloadedBytesUpdate
                    ( remoteContact, IOBehavior, m_discoveryService.ProtocolVersion ).ConfigureAwait( continueOnCapturedContext: false );

            cancellationToken.ThrowIfCancellationRequested();

            Boolean isRightResponse = IsRightDownloadFileResponse( lastRequest, response, rpcError );
            if ( isRightResponse )
            {
                //if it is small file, we won't need to use seek, because we need to write from 0 position
                lock ( m_lockWriteFile )
                {
                    if ( fileStream.Position != (Int64)lastRequest.ChunkRange.Start )
                    {
                        if ( fileStream.CanSeek )
                        {
                            fileStream.Seek( offset: (Int64)lastRequest.ChunkRange.Start, SeekOrigin.Begin );
                        }
                        else
                        {
                            throw new InvalidOperationException( $"Cannot seek in file {lastRequest.FullPathToFile}" );
                        }
                    }

                    fileStream.Write( response.Chunk, offset: 0, response.Chunk.Length );
                    downloadProgress?.Report( lastRequest.ChunkRange );
                }

                isWritenInFile = true;
            }

            return (isWritenInFile, lastRequest);
        }

        private Boolean IsFinishedDownload( UInt64 start, UInt64 end ) =>
            start >= end;

        //TODO replace in DownloadFileRequest
        private Boolean IsRightDownloadFileResponse( DownloadFileRequest request, DownloadFileResponse response, RpcError rpcError )
        {
            Boolean isReceivedRequiredRange = ( !rpcError.HasError ) && ( response.IsRightBucket ) &&
                ( response.FileExists ) && ( (Int32)( request.ChunkRange.End - request.ChunkRange.Start ) == response.Chunk.Length - 1 );

            //file can be changed in remote contact during download process
            Boolean isTheSameFileInRemoteContact;
            if ( isReceivedRequiredRange )
            {
                isTheSameFileInRemoteContact = ( response.FileVersion == request.FileVersion );
            }
            else
            {
                //TODO add log if file doesn't exist
                isTheSameFileInRemoteContact = false;
            }

            return ( isReceivedRequiredRange ) && ( isTheSameFileInRemoteContact );
        }

        private async Task DownloadBigFileAsync( IEnumerable<Contact> contactsWithFile, DownloadFileRequest initialRequest, CancellationToken cancellationToken, IProgress<ChunkRange> downloadProgress )
        {
            //create temp file in order to another contacts don't download it
            String tempFullPath = m_downloadedFile.TempFullFileName( initialRequest.FullPathToFile );

            Boolean isDownloadedFile = false;
            ConcurrentDictionary<Contact, DownloadFileRequest> dictContactsWithRequest = null;

            tempFullPath = m_downloadedFile.UniqueTempFullFileName( tempFullPath );

            IList<Contact> contactsWithFileList = contactsWithFile.ToList();
            if ( contactsWithFileList.Count == 0 )
            {
                throw new InvalidOperationException( MESS_IF_FILE_DOESNT_EXIST_IN_ANY_NODE );
            }
            else
            {
                dictContactsWithRequest = ContactsAndRequestsToDownload( initialRequest, contactsWithFileList, cancellationToken );

                try
                {
                    using ( FileStream fileStream = File.OpenWrite( tempFullPath ) )
                    {
                        m_downloadedFile.SetTempFileAttributes( tempFullPath, fileStream.SafeFileHandle );

                        //we need to set fileStream.Length only at start downloading file. 
                        Int64 bytesStreamCount = (Int64)initialRequest.ChunkRange.Total;

                        DriveInfo driveInfo = new DriveInfo( driveName: tempFullPath[ 0 ].ToString() );
                        Boolean isAvailableToDownload = ( driveInfo.IsReady ) && ( driveInfo.AvailableFreeSpace > (Int64)initialRequest.ChunkRange.Total );

                        if ( ( fileStream.Length != bytesStreamCount ) && ( isAvailableToDownload ) )
                        {
                            fileStream.SetLength( bytesStreamCount );
                        }
                        else
                        {
                            throw new NotEnoughDriveSpaceException( initialRequest.FullPathToFile );
                        }

                        ExecutionDataflowBlockOptions parallelOptions = ParallelOptions( cancellationToken );

                        do
                        {
                            //Producer/consumer pattern:
                            //current thread is producer and  produce contact and request to
                            //download some parts of the file using method ActionBlock.Post. 
                            //Consumers send request, receive response and write accepted bytes in stream.
                            ActionBlock<KeyValuePair<Contact, DownloadFileRequest>> downloadProcess = new ActionBlock<KeyValuePair<Contact, DownloadFileRequest>>( async ( contactWithRequest ) =>
                            {
                                DownloadFileRequest updatedRequest = await DownloadProcessBigFileAsync(
                                    contactWithRequest.Key,
                                    contactWithRequest.Value,
                                    fileStream,
                                    cancellationToken,
                                    downloadProgress
                                );

                                dictContactsWithRequest.AddOrUpdate( contactWithRequest.Key, updatedRequest, ( contact, oldRequest ) => updatedRequest );
                            }, parallelOptions );

                            foreach ( KeyValuePair<Contact, DownloadFileRequest> contactWithRequest in dictContactsWithRequest )
                            {
                                downloadProcess.Post( contactWithRequest );
                            }

                            //Signals that we will not post more contactWithRequest. 
                            //downloadProcess.Completion will never be completed without this calling
                            downloadProcess.Complete();

                            //await completion of download all file
                            await downloadProcess.Completion.ConfigureAwait( false );

                            isDownloadedFile = IsDownloadedFile( tempFullPath, (UInt64)bytesStreamCount,
                                downloadedBytesByEachContact: dictContactsWithRequest.Values.Select( c => c.CountDownloadedBytes ) );
                            if ( !isDownloadedFile )
                            {
                                //get new contacts and requests considering the previous download
                                dictContactsWithRequest = ContactsWithRequestToDownload( dictContactsWithRequest, cancellationToken );

                                if ( dictContactsWithRequest.Count == 0 )
                                {
                                    throw new FilePartiallyDownloadedException( MESS_IF_FILE_DOESNT_EXIST_IN_ANY_NODE );
                                }
                            }
                        }
                        while ( !isDownloadedFile );
                    }
                }
                catch ( FilePartiallyDownloadedException ex )
                {
                    //it can't be set in method Download.DownloadProcessBigFileAsync, 
                    //because it doesn't have all ranges
                    ex.Ranges = dictContactsWithRequest?.Select( c => c.Value.ChunkRange ).ToList();

                    throw ex;
                }
            }

            if ( !cancellationToken.IsCancellationRequested )
            {
                m_downloadedFile.RenameFile( tempFullPath, initialRequest.FullPathToFile );
                File.SetAttributes( initialRequest.FullPathToFile, FileAttributes.Normal );
            }
        }

        private async Task<DownloadFileRequest> DownloadProcessBigFileAsync( Contact contact, DownloadFileRequest request, Stream fileStream, CancellationToken cancellationToken, IProgress<ChunkRange> downloadProgress )
        {
            DownloadFileRequest updatedRequest;

            if ( request.ChunkRange.TotalPerContact <= Constants.MAX_CHUNK_SIZE )
            {
                (_, updatedRequest) = await DownloadProcessSmallChunkAsync( contact, request, fileStream, cancellationToken, downloadProgress ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else
            {
                updatedRequest = await DownloadBigTotalPerContactBytesAsync( contact, request, fileStream, cancellationToken, downloadProgress ).ConfigureAwait( false );
            }

            cancellationToken.ThrowIfCancellationRequested();

            return updatedRequest;
        }

        private async Task<DownloadFileRequest> DownloadBigTotalPerContactBytesAsync( Contact remoteContact, DownloadFileRequest sampleRequest, Stream fileStream, CancellationToken cancellationToken, IProgress<ChunkRange> downloadProgress )
        {
            UInt32 maxChunkSize = Constants.MAX_CHUNK_SIZE;
            ChunkRange initialContantRange = sampleRequest.ChunkRange;
            DownloadFileRequest lastRequest = (DownloadFileRequest)sampleRequest.Clone();
            UInt64 start = lastRequest.ChunkRange.Start;
            Boolean isRightResponse = true;

            for ( UInt64 end = start + maxChunkSize - 1;
                 ( !IsFinishedDownload( start, end ) ) && ( isRightResponse );
                 start = end + 1, end = ( ( end + maxChunkSize ) < initialContantRange.TotalPerContact ) ? ( end + maxChunkSize ) : initialContantRange.TotalPerContact - 1 )
            {
                lastRequest.ChunkRange.Start = start;
                lastRequest.ChunkRange.End = end;

                (DownloadFileResponse response, RpcError rpcError) = await lastRequest.ResultAsyncWithCountDownloadedBytesUpdate( remoteContact, IOBehavior, m_discoveryService.ProtocolVersion ).ConfigureAwait( false );

                cancellationToken.ThrowIfCancellationRequested();

                isRightResponse = IsRightDownloadFileResponse( lastRequest, response, rpcError );
                if ( isRightResponse )
                {
                    //we use lock, because method fileStream.Write can be invoked from wrong position 
                    //(in case method seek is called by another thread)
                    lock ( m_lockWriteFile )
                    {
                        if ( fileStream.Position != (Int64)lastRequest.ChunkRange.Start )
                        {
                            if ( fileStream.CanSeek )
                            {
                                fileStream.Seek( offset: (Int64)lastRequest.ChunkRange.Start, SeekOrigin.Begin );
                            }
                            else
                            {
                                throw new InvalidOperationException( $"Cannot seek in file {lastRequest.FullPathToFile}" );
                            }
                        }

                        fileStream.Write( response.Chunk, offset: 0, response.Chunk.Length );
                        downloadProgress?.Report( lastRequest.ChunkRange );
                    }
                }
            }

            return lastRequest;
        }

        private ConcurrentDictionary<Contact, DownloadFileRequest> ContactsAndRequestsToDownload( DownloadFileRequest sampleRequest, IList<Contact> contactsWithFile, CancellationToken cancellationToken )
        {
            ConcurrentDictionary<Contact, DownloadFileRequest> contactsAndRequests = new ConcurrentDictionary<Contact, DownloadFileRequest>();

            UInt32 maxChunkSize = Constants.MAX_CHUNK_SIZE;

            UInt64 сountUndistributedBytes = sampleRequest.ChunkRange.Total;
            UInt64 lastPartBytesOfContact = 0;

            UInt32 partBytesOfContact = (UInt32)сountUndistributedBytes / (UInt32)contactsWithFile.Count;
            if ( partBytesOfContact < maxChunkSize )
            {
                partBytesOfContact = maxChunkSize;
            }

            Int32 numChunk = 0;
            for ( UInt32 numContact = 0;
                numContact < contactsWithFile.Count && сountUndistributedBytes > 0;
                numContact++, сountUndistributedBytes -= partBytesOfContact )
            {
                cancellationToken.ThrowIfCancellationRequested();

                if ( ( сountUndistributedBytes < maxChunkSize ) || ( numContact == contactsWithFile.Count - 1 ) )
                {
                    partBytesOfContact = (UInt32)сountUndistributedBytes;
                }

                DownloadFileRequest request = (DownloadFileRequest)sampleRequest.Clone();
                request.ChunkRange.Start = lastPartBytesOfContact;
                request.ChunkRange.TotalPerContact = partBytesOfContact;
                lastPartBytesOfContact = request.ChunkRange.Start + request.ChunkRange.TotalPerContact;

                List<Int32> numsUndownloadedChunk = NumsChunk( request.ChunkRange.Start, lastPartBytesOfContact, maxChunkSize, ref numChunk );
                request.ChunkRange.NumsUndownloadedChunk.AddRange( numsUndownloadedChunk );

                contactsAndRequests.TryAdd( contactsWithFile[ (Int32)numContact ], request );
            }

            return contactsAndRequests;
        }

        private List<Int32> NumsChunk( UInt64 start, UInt64 totalBytesPerContact, UInt32 maxChunkSize, ref Int32 lastNumChunk )
        {
            List<Int32> numsChunk = new List<Int32>();

            for ( UInt64 chunk = start; chunk < totalBytesPerContact; chunk += maxChunkSize, lastNumChunk++ )
            {
                numsChunk.Add( lastNumChunk );
            }

            return numsChunk;
        }

        private Boolean IsDownloadedFile( String fullPathToFile, UInt64 countOfBytes, IEnumerable<UInt64> downloadedBytesByEachContact )
        {
            Boolean isRightDownloaded;

            if ( countOfBytes == (UInt64)downloadedBytesByEachContact.Sum( c => (Int64)c ) && File.Exists( fullPathToFile ) )
            {
                isRightDownloaded = true;
            }
            else
            {
                isRightDownloaded = false;
            }

            return isRightDownloaded;
        }

        /// <returns>
        /// <see cref="Contact"/>s with <see cref="DownloadFileRequest"/>s with <see cref="ChunkRange"/>s which aren't download before
        /// </returns>
        private ConcurrentDictionary<Contact, DownloadFileRequest> ContactsWithRequestToDownload( ConcurrentDictionary<Contact, DownloadFileRequest> contactsWithRequest, CancellationToken cancellationToken )
        {
            List<DownloadFileRequest> oldRequests = contactsWithRequest.Values.ToList();

            UInt64 сountUndistributedBytes = 0;
            foreach ( DownloadFileRequest oldRequest in oldRequests )
            {
                if ( oldRequest.ChunkRange.TotalPerContact >= oldRequest.CountDownloadedBytes )
                {
                    сountUndistributedBytes += oldRequest.ChunkRange.TotalPerContact - oldRequest.CountDownloadedBytes;
                }
                else
                {
                    throw new InvalidOperationException( "Something was wrong during download process: " +
                        "too many bytes are downloaded from certain contact" );
                }
            }

            if ( сountUndistributedBytes == 0 )
            {
                throw new InvalidOperationException( $"All bytes was downloaded, but method {nameof( ContactsWithRequestToDownload )} is called" );
            }
            else
            {
                ConcurrentDictionary<Contact, DownloadFileRequest> newContactsWithRequest = new ConcurrentDictionary<Contact, DownloadFileRequest>();
                UInt32 maxChunkSize = Constants.MAX_CHUNK_SIZE;
                DownloadFileRequest sampleRequest = oldRequests.First();

                List<Contact> contactsForRetryDownload = ContactsForRetryDownload( sampleRequest.BucketId ).ToList();
                List<Contact> contactsWithFile = ContactsWithFile( contactsForRetryDownload, sampleRequest, cancellationToken ).ToList();

                for ( Int32 numContact = 0, numRequest = 0;
                     ( numRequest < oldRequests.Count ) && ( сountUndistributedBytes > 0 );
                     сountUndistributedBytes -= oldRequests[ numRequest ].ChunkRange.TotalPerContact, numContact++, numRequest++ )
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    DownloadFileRequest request = oldRequests[ numRequest ];

                    //get before update, because DownloadFileRequest.ChunkRange.TotalPerContact will be decreased
                    Boolean wasDownloadedAllBytes = request.WasDownloadedAllBytes;
                    request.Update();

                    while ( wasDownloadedAllBytes )
                    {
                        numRequest++;
                        request = oldRequests[ numRequest ];

                        wasDownloadedAllBytes = request.WasDownloadedAllBytes;
                        request.Update();
                    }

                    //maybe it should be call of method DownloadFileAsync in this if
                    if ( ( сountUndistributedBytes - request.ChunkRange.TotalPerContact > 0 ) && ( numContact == contactsWithFile.Count - 1 ) )
                    {
                        throw new FilePartiallyDownloadedException( "Contacts have strange behavior. Cannot normally download file" );
                    }

                    if ( request.ChunkRange.TotalPerContact <= maxChunkSize )
                    {
                        request.ChunkRange.End = request.ChunkRange.Start + request.ChunkRange.TotalPerContact;
                    }

                    newContactsWithRequest.TryAdd( contactsWithFile[ numContact ], request );
                }

                return newContactsWithRequest;
            }
        }

        /// <returns>
        /// Contact which is the same bucket and answered last request
        /// </returns>
        private IEnumerable<Contact> ContactsForRetryDownload(String localBucketName)
        {
            IEnumerable<Contact> contactsForRetryDownload;
            try
            {
                IEnumerable<Contact> contactsInSameBucket = ContactsInSameBucket( localBucketName );

                Dht dht = NetworkEventInvoker.DistributedHashTable( m_discoveryService.ProtocolVersion );
                contactsForRetryDownload = contactsInSameBucket.Where( c =>
                {
                    Boolean shouldCommunicateInDownload = !dht.EvictionCount.ContainsKey(c.KadId.Value);
                    if(!shouldCommunicateInDownload)
                    {
                        shouldCommunicateInDownload = dht.EvictionCount[ c.KadId.Value ] == 0;
                    }

                    return shouldCommunicateInDownload;
                } );

                if ( contactsForRetryDownload.Count() < MIN_CONTACT_FOR_RETRY_DOWNLOAD )
                {
                    contactsForRetryDownload = contactsInSameBucket;
                }
            }
            catch (Exception ex)
            {
                String logRecord = Display.StringWithAttention( ex.ToString() );
                LoggingService.LogError( logRecord );
                throw;
            }
            

            return contactsForRetryDownload;
        }
    }
}
