using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace LUC.DiscoveryServices.Kademlia.Downloads
{
    public partial class Download
    {
        private async ValueTask DownloadSmallFileAsync( 
            IEnumerable<Contact> onlineContacts, 
            DownloadFileRequest initialRequest, 
            CancellationToken cancellationToken, 
            IProgress<FileDownloadProgressArgs> downloadProgress 
        ){
            //we need to cancel requesting another contacts whether they have file when we have downloaded it
            CancellationTokenSource cancelSource = new CancellationTokenSource();
            IEnumerable<Contact> contactsWithFile = ContactsWithFile( onlineContacts, initialRequest, cancelSource.Token, CONTACT_COUNT_WITH_FILE_CAPACITY );

            DownloadFileRequest request = (DownloadFileRequest)initialRequest.Clone();
            request.ChunkRange.End = request.ChunkRange.Total - 1;
            request.ChunkRange.NumsUndownloadedChunk.Add( 0 );

            Boolean isRightDownloaded = false;

            using ( Stream fileStream = File.OpenWrite( request.FullFileName ) )
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
                //m_downloadedFile.TryDeleteFile( request.FullFileName );
                throw new InvalidOperationException( $"Cannot download small file {request.FullFileName}. Contact count which have this file = {contactsWithFile.Count()}" );
            }
        }

        /// <summary>
        /// <paramref name="downloadFileRequest"/> should be absolutelly initialized outside this method
        /// </summary>
        /// <returns>
        /// First value returns whether <see cref="DownloadFileRequest.CountDownloadedBytes"/> is writen in <paramref name="fileStream"/>. The second returns <paramref name="downloadFileRequest"/> with updated <see cref="DownloadFileRequest.CountDownloadedBytes"/>, <paramref name="downloadFileRequest"/> will not be changed
        /// </returns>
        private async ValueTask<(Boolean isBytesWritenInStream, DownloadFileRequest updatedRequest)> DownloadProcessSmallChunkAsync( 
            Contact remoteContact,
            DownloadFileRequest downloadFileRequest, 
            Stream fileStream, 
            CancellationToken cancellationToken, 
            IProgress<FileDownloadProgressArgs> downloadProgress 
        ){
            Boolean isWritenInFile = false;
            DownloadFileRequest lastRequest = (DownloadFileRequest)downloadFileRequest.Clone();

            (DownloadFileResponse response, RpcError rpcError, Boolean isRightResponse) = await lastRequest.ResultAsyncWithCountDownloadedBytesUpdate
                    ( remoteContact, IOBehavior, m_discoveryService.ProtocolVersion ).ConfigureAwait( continueOnCapturedContext: false );

            cancellationToken.ThrowIfCancellationRequested();

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
                            String messageException = $"Cannot seek in file {lastRequest.FullFileName}";
                            if (lastRequest.ChunkRange.Total > Constants.MAX_CHUNK_SIZE)
                            {
                                throw new FilePartiallyDownloadedException( messageException );
                            }
                            else
                            {
                                throw new InvalidOperationException( messageException );
                            }
                        }
                    }

                    fileStream.Write( response.Chunk, offset: 0, response.Chunk.Length );
                    downloadProgress?.Report( new FileDownloadProgressArgs( (ChunkRange)lastRequest.ChunkRange.Clone(), lastRequest.FullFileName ) );
                }

                isWritenInFile = true;
            }

            return (isWritenInFile, lastRequest);
        }

        private Boolean IsFinishedDownload( UInt64 start, UInt64 end ) =>
            start >= end;        

        private async ValueTask DownloadBigFileAsync( 
            IEnumerable<Contact> contactsWithFile, 
            DownloadFileRequest initialRequest, 
            CancellationToken cancellationToken, 
            IProgress<FileDownloadProgressArgs> downloadProgress 
        ){
            //create temp file in order to another contacts don't download it
            String tempFullFileName = m_downloadedFile.TempFullFileName( initialRequest.FullFileName );

            Boolean isDownloadedFile = false;
            ConcurrentDictionary<Contact, DownloadFileRequest> dictContactsWithRequest = null;

            tempFullFileName = m_downloadedFile.UniqueTempFullFileName( tempFullFileName );

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
                    using ( FileStream fileStream = File.OpenWrite( tempFullFileName ) )
                    {
                        m_downloadedFile.SetTempFileAttributes( tempFullFileName, fileStream.SafeFileHandle );

                        Int64 bytesStreamCount = (Int64)initialRequest.ChunkRange.Total;

                        DriveInfo driveInfo = new DriveInfo( driveName: tempFullFileName[ 0 ].ToString() );
                        Boolean isAvailableToDownload = driveInfo.AvailableFreeSpace > (Int64)initialRequest.ChunkRange.Total;

                        //we need to set fileStream.Length only at start downloading file. 
                        if ( ( fileStream.Length != bytesStreamCount ) && ( isAvailableToDownload ) )
                        {
                            fileStream.SetLength( bytesStreamCount );
                        }
                        else if ( !isAvailableToDownload )
                        {
                            throw new NotEnoughDriveSpaceException( initialRequest.FullFileName );
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
                                ).ConfigureAwait(false);

                                dictContactsWithRequest.AddOrUpdate( contactWithRequest.Key, updatedRequest, ( contact, oldRequest ) => updatedRequest );
                            }, parallelOptions );

                            Parallel.ForEach( dictContactsWithRequest, ( contactWithRequest ) => downloadProcess.Post( contactWithRequest ) );

                            //Signals that we will not post more contactWithRequest. 
                            //downloadProcess.Completion will never be completed without this calling
                            downloadProcess.Complete();

                            //await completion of download all file
                            await downloadProcess.Completion.ConfigureAwait( false );

                            UInt64 bytesCountWhichShouldBeDownloaded = (UInt64)dictContactsWithRequest.Values.Select( c => (Int64)c.ChunkRange.TotalPerContact ).Sum();
                            IEnumerable<UInt64> downloadedBytesByEachContact = dictContactsWithRequest.Values.Select( c => c.CountDownloadedBytes );

                            isDownloadedFile = IsDownloadedFile( tempFullFileName, bytesCountWhichShouldBeDownloaded, downloadedBytesByEachContact );
                            if ( !isDownloadedFile )
                            {
                                //get new contacts and requests considering the previous download
                                dictContactsWithRequest = ContactsWithRequestToDownload( dictContactsWithRequest, cancellationToken );
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

            cancellationToken.ThrowIfCancellationRequested();

            m_downloadedFile.RenameFile( tempFullFileName, initialRequest.FullFileName );
            File.SetAttributes( initialRequest.FullFileName, FileAttributes.Normal );
        }

        private async ValueTask<DownloadFileRequest> DownloadProcessBigFileAsync( 
            Contact contact, 
            DownloadFileRequest request, 
            Stream fileStream, 
            CancellationToken cancellationToken, 
            IProgress<FileDownloadProgressArgs> downloadProgress 
        ){
            DownloadFileRequest updatedRequest;

            if ( request.ChunkRange.TotalPerContact <= Constants.MAX_CHUNK_SIZE )
            {
                request.ChunkRange.End = request.ChunkRange.Start + request.ChunkRange.TotalPerContact - 1;
                (_, updatedRequest) = await DownloadProcessSmallChunkAsync( contact, request, fileStream, cancellationToken, downloadProgress ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else
            {
                updatedRequest = await DownloadBigTotalPerContactBytesAsync( contact, request, fileStream, cancellationToken, downloadProgress ).ConfigureAwait( false );
            }

            cancellationToken.ThrowIfCancellationRequested();

            return updatedRequest;
        }

        private async ValueTask<DownloadFileRequest> DownloadBigTotalPerContactBytesAsync( 
            Contact remoteContact, 
            DownloadFileRequest sampleRequest, 
            Stream fileStream, 
            CancellationToken cancellationToken, 
            IProgress<FileDownloadProgressArgs> downloadProgress 
        ){
            UInt32 maxChunkSize = Constants.MAX_CHUNK_SIZE;

            ChunkRange initialContantRange = sampleRequest.ChunkRange;
            DownloadFileRequest lastRequest = (DownloadFileRequest)sampleRequest.Clone();

            UInt64 finallyEnd = initialContantRange.TotalPerContact - 1 + initialContantRange.Start;
            UInt64 start = lastRequest.ChunkRange.Start;

            //is set to true to start circle execution
            Boolean isRightResponse = true;

            DownloadFileResponse response;
            for ( UInt64 end = start + maxChunkSize - 1;
                 ( !IsFinishedDownload( start, end ) ) && ( isRightResponse );
                 start = end + 1, end = ( ( end + maxChunkSize ) <= finallyEnd ) ? ( end + maxChunkSize ) : finallyEnd )
            {
                lastRequest.ChunkRange.Start = start;
                lastRequest.ChunkRange.End = end;

                (response, _, isRightResponse) = await lastRequest.ResultAsyncWithCountDownloadedBytesUpdate( remoteContact, IOBehavior, m_discoveryService.ProtocolVersion ).ConfigureAwait( continueOnCapturedContext: false );

                cancellationToken.ThrowIfCancellationRequested();

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
                                throw new FilePartiallyDownloadedException( $"Cannot seek in file {lastRequest.FullFileName}" );
                            }
                        }

                        fileStream.Write( response.Chunk, offset: 0, response.Chunk.Length );
                        downloadProgress?.Report( new FileDownloadProgressArgs( (ChunkRange)lastRequest.ChunkRange.Clone(), lastRequest.FullFileName ) );
                    }
                }
            }

            return lastRequest;
        }

        private ConcurrentDictionary<Contact, DownloadFileRequest> ContactsAndRequestsToDownload( 
            DownloadFileRequest sampleRequest, 
            IList<Contact> contactsWithFile, 
            CancellationToken cancellationToken 
        ){
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
                request.ChunkRange.NumsUndownloadedChunk = numsUndownloadedChunk;

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
            UInt64 downloadedBytesCount = (UInt64)downloadedBytesByEachContact.Sum( c => (Int64)c );
            Boolean isRightDownloaded = ( countOfBytes == downloadedBytesCount ) && ( File.Exists( fullPathToFile ) );

            return isRightDownloaded;
        }

        /// <returns>
        /// <see cref="Contact"/>s with <see cref="DownloadFileRequest"/>s with <see cref="ChunkRange"/>s which aren't downloaded before
        /// </returns>
        private ConcurrentDictionary<Contact, DownloadFileRequest> ContactsWithRequestToDownload( 
            ConcurrentDictionary<Contact, DownloadFileRequest> contactsWithRequest, 
            CancellationToken cancellationToken 
        ){
            List<DownloadFileRequest> oldRequests = contactsWithRequest.Values.ToList();

            List<DownloadFileRequest> requestsWithUndownloadedBytes = new List<DownloadFileRequest>();
            UInt64 сountUndistributedBytes = 0;
            List<Contact> contactsWhichHaveFile = new List<Contact>();

            foreach ( KeyValuePair<Contact, DownloadFileRequest> contactAndRequest in contactsWithRequest )
            {
                DownloadFileRequest request = contactAndRequest.Value;
                Contact contactWithFile = contactAndRequest.Key;

                UInt64 undownloadedByteCount = request.ChunkRange.TotalPerContact - request.CountDownloadedBytes;

                if ( undownloadedByteCount > 0 )
                {
                    сountUndistributedBytes += undownloadedByteCount;
                    requestsWithUndownloadedBytes.Add( request );
                }
                else if (undownloadedByteCount == 0)
                {
                    contactsWhichHaveFile.Add( contactWithFile );
                }
                else
                {
                    throw new FilePartiallyDownloadedException( "Something was wrong during download process: " +
                        "too many bytes are downloaded from certain contact" );
                }
            }

            if ( сountUndistributedBytes == 0 )
            {
                throw new FilePartiallyDownloadedException( $"All bytes was downloaded, but method {nameof( ContactsWithRequestToDownload )} is called" );
            }
            else
            {
                DownloadFileRequest sampleRequest = oldRequests.First();

                if ( contactsWhichHaveFile.Count < requestsWithUndownloadedBytes.Count )
                {
                    Int32 minContactCount = requestsWithUndownloadedBytes.Count - contactsWhichHaveFile.Count;

                    IEnumerable<Contact> otherOnlineContacts = m_discoveryService.OnlineContacts().Except( contactsWhichHaveFile );
                    IEnumerable<Contact> otherContactsInSameBucket = ContactsInSameBucket( otherOnlineContacts, sampleRequest.LocalBucketId );

                    String messageException = "Too few contacts have file to continue download";
                    if (minContactCount <= otherContactsInSameBucket.Count())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        List<Contact> newContactsWithFile = ContactsWithFile( otherContactsInSameBucket, sampleRequest, cancellationToken, minContactCount ).ToList();
                        contactsWhichHaveFile.AddRange( newContactsWithFile );

                        if( contactsWhichHaveFile.Count < requestsWithUndownloadedBytes.Count )
                        {
                            throw new FilePartiallyDownloadedException( messageException );
                        }
                    }
                    else
                    {
                        throw new FilePartiallyDownloadedException( messageException );
                    }
                }

                ConcurrentDictionary<Contact, DownloadFileRequest> newContactsWithRequest = new ConcurrentDictionary<Contact, DownloadFileRequest>();

                for ( Int32 numContact = 0, numRequest = 0;
                      ( numRequest < requestsWithUndownloadedBytes.Count ) && ( сountUndistributedBytes > 0 );
                      сountUndistributedBytes -= requestsWithUndownloadedBytes[ numRequest ].ChunkRange.TotalPerContact, numContact++, numRequest++ )
                {
                    DownloadFileRequest request = requestsWithUndownloadedBytes[ numRequest ];
                    request.Update();

                    newContactsWithRequest.TryAdd( contactsWhichHaveFile[ numContact ], request );
                }

                return newContactsWithRequest;
            }
        }

        /// <returns>
        /// Contacts which is the same bucket and answered last request
        /// </returns>
        private IEnumerable<Contact> ContactsForRetryDownload( IEnumerable<Contact> contacts, String localBucketName, Int32 minContactCount )
        {
            IEnumerable<Contact> contactsInSameBucket = ContactsInSameBucket( contacts, localBucketName );

            Dht dht = NetworkEventInvoker.DistributedHashTable( m_discoveryService.ProtocolVersion );
            IEnumerable<Contact> contactsForRetryDownload = contactsInSameBucket.Where( c =>
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
