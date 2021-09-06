using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Exceptions;
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

namespace LUC.DiscoveryService
{
    public partial class Download
    {
        private async Task DownloadSmallFileAsync(IList<Contact> contactsWithFile, DownloadFileRequest initialRequest, CancellationToken cancellationToken)
        {
            var request = (DownloadFileRequest)initialRequest.Clone();
            request.Range.End = request.Range.Total - 1;
            request.Range.NumsUndownloadedChunk.Add(0);

            using (Stream fileStream = File.OpenWrite(request.FullPathToFile))
            {
                Boolean isRightDownloaded = false;
                RpcError rpcError = new RpcError();

                for (Int32 numContact = 0; (numContact < contactsWithFile.Count) && (!isRightDownloaded); numContact++)
                {
                    //here small chunk is full file, because this file has length less than Constants.MaxChunkSize
                    (isRightDownloaded, _) = await DownloadProcessSmallChunkAsync(contactsWithFile[numContact], request, fileStream, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

                    cancellationToken.ThrowIfCancellationRequested();

                    if ((numContact == contactsWithFile.Count - 1) && (!isRightDownloaded))
                    {
                        downloadedFile.TryDeleteFile(request.FullPathToFile);
                    }
                }
            }
        }

        /// <summary>
        /// <paramref name="downloadFileRequest"/> should be absolutelly initialized outside this method
        /// </summary>
        /// <returns>
        /// First value returns whether <see cref="DownloadFileRequest.CountDownloadedBytes"/> is writen in <paramref name="fileStream"/>. The second returns <paramref name="downloadFileRequest"/> with updated <see cref="DownloadFileRequest.CountDownloadedBytes"/>, <paramref name="downloadFileRequest"/> will not be changed
        /// </returns>
        private async Task<(Boolean, DownloadFileRequest)> DownloadProcessSmallChunkAsync(Contact remoteContact,
            DownloadFileRequest downloadFileRequest, Stream fileStream, CancellationToken cancellationToken)
        {
            var isWritenInFile = false;
            DownloadFileRequest lastRequest = (DownloadFileRequest)downloadFileRequest.Clone();

            (DownloadFileResponse response, RpcError rpcError) = await lastRequest.ResultAsyncWithCountDownloadedBytesUpdate
                    (remoteContact, IOBehavior, discoveryService.ProtocolVersion).ConfigureAwait(continueOnCapturedContext: false);

            cancellationToken.ThrowIfCancellationRequested();

            Boolean isRightResponse = IsRightDownloadFileResponse(lastRequest, response, rpcError);
            if (isRightResponse)
            {
                //if it is small file, we won't need to use seek, because we need to write from 0 position
                if (fileStream.Length > Constants.MaxChunkSize)
                {
                    if(fileStream.CanSeek)
                    {
                        lock (lockWriteFile)
                        {
                            fileStream.Seek(offset: (Int64)lastRequest.Range.Start, SeekOrigin.Begin);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot seek in file {lastRequest.FullPathToFile}");
                    }
                }

                fileStream.Write(response.Chunk, offset: 0, response.Chunk.Length);
                isWritenInFile = true;
            }

            return (isWritenInFile, lastRequest);
        }

        private Boolean IsFinishedDownload(UInt64 start, UInt64 end) =>
            start >= end;

        private Boolean IsRightDownloadFileResponse(DownloadFileRequest request, DownloadFileResponse response, RpcError rpcError)
        {
            Boolean isReceivedRequiredRange = (!rpcError.HasError) && (response.IsRightBucket) &&
                (response.FileExists) && ((Int32)(request.Range.End - request.Range.Start) == response.Chunk.Length - 1);

            //file can be changed in remote contact during download process
            Boolean isTheSameFileInRemoteContact;
            if (isReceivedRequiredRange)
            {
                isTheSameFileInRemoteContact = (response.FileVersion == request.FileVersion);
            }
            else
            {
                isTheSameFileInRemoteContact = false;
            }

            return (isReceivedRequiredRange) && (isTheSameFileInRemoteContact);
        }

        private async Task DownloadBigFileAsync(IList<Contact> contactsWithFile, DownloadFileRequest initialRequest, CancellationToken cancellationToken)
        {
            //create temp file in order to another contacts don't download it
            String tempFullPath = downloadedFile.TempFullFileName(initialRequest.FullPathToFile);

            Boolean isDownloadedFile = false;
            Int32 countAttemptToDownload = 0;
            do
            {
                try
                {
                    if (countAttemptToDownload == 0)
                    {
                        tempFullPath = downloadedFile.UniqueTempFullFileName(tempFullPath);
                    }

                    ConcurrentDictionary<Contact, DownloadFileRequest> dictContactsWithRequest = null;
                    if ((countAttemptToDownload == 0) || (!File.Exists(tempFullPath)))
                    {
                        dictContactsWithRequest = ContactsAndRequestsToDownload(initialRequest, contactsWithFile, cancellationToken);
                    }

                    //if file was renamed during download processes by user, it will be created again
                    using (var fileStream = File.OpenWrite(tempFullPath))
                    {
                        downloadedFile.SetTempFileAttributes(tempFullPath, fileStream.SafeFileHandle);

                        Int64 bytesStreamCount = (Int64)initialRequest.Range.Total;
                        if (fileStream.Length != bytesStreamCount)
                        {
                            fileStream.SetLength(bytesStreamCount);
                        }

                        var parallelOptions = new ExecutionDataflowBlockOptions
                        {
                            MaxDegreeOfParallelism = Constants.MAX_THREADS,
                            CancellationToken = cancellationToken
                        };

                        //Producer/consumer pattern:
                        //current thread is producer and  produce contact and request to
                        //download some parts of the file using method ActionBlock.Post. 
                        //Consumers send request, receive response and write accepted bytes in stream.
                        var downloadProcess = new ActionBlock<KeyValuePair<Contact, DownloadFileRequest>>(async (contactWithRequest) =>
                        {
                            DownloadFileRequest updatedRequest = await DownloadProcessBigFileAsync(contactWithRequest.Key, contactWithRequest.Value,
                                fileStream, cancellationToken);

                            dictContactsWithRequest.AddOrUpdate(contactWithRequest.Key, updatedRequest, (contact, oldRequest) => updatedRequest);
                        }, parallelOptions);

                        foreach (var contactWithRequest in dictContactsWithRequest)
                        {
                            downloadProcess.Post(contactWithRequest);
                        }

                        //Signals that we will not post more contactWithRequest
                        //and also here is start of the download processes
                        downloadProcess.Complete();

                        //await completion of download all file
                        await downloadProcess.Completion.ConfigureAwait(false);

                        isDownloadedFile = IsDownloadedFile(tempFullPath, (UInt64)bytesStreamCount,
                            downloadedBytesByEachContact: dictContactsWithRequest.Values.Select(c => c.CountDownloadedBytes));
                        if (!isDownloadedFile)
                        {
                            //get new contacts and requests considering the previous download
                            dictContactsWithRequest = await ContactsWithRequestToDownloadAsync(dictContactsWithRequest,
                                cancellationToken).ConfigureAwait(false);

                            if (dictContactsWithRequest.Count == 0)
                            {
                                throw new FilePartiallyDownloadedException(MessIfThisFileDoesntExistInAnyNode);
                            }
                        }
                    }
                }
                catch (AggregateException ex)
                {
                    LoggingService.LogError(ex.ToString());
                }
            }
            while (!isDownloadedFile);


            if (!cancellationToken.IsCancellationRequested)
            {
                downloadedFile.RenameFile(tempFullPath, initialRequest.FullPathToFile);
                File.SetAttributes(initialRequest.FullPathToFile, FileAttributes.Normal);
            }
            else
            {
                downloadedFile.TryDeleteFile(tempFullPath);
            }
        }

        private async Task<DownloadFileRequest> DownloadProcessBigFileAsync(Contact contact, DownloadFileRequest request, Stream fileStream, CancellationToken cancellationToken)
        {
            DownloadFileRequest updatedRequest;

            if (request.Range.TotalPerContact <= Constants.MaxChunkSize)
            {
                (_, updatedRequest) = await DownloadProcessSmallChunkAsync(contact, request, fileStream, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }
            else
            {
                updatedRequest = await DownloadBigTotalPerContactBytesAsync(contact, request, fileStream, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return updatedRequest;
        }

        private async Task<DownloadFileRequest> DownloadBigTotalPerContactBytesAsync(Contact remoteContact, DownloadFileRequest sampleRequest, Stream fileStream, CancellationToken cancellationToken)
        {
            UInt32 maxChunkSize = Constants.MaxChunkSize;
            Range initialContantRange = sampleRequest.Range;
            DownloadFileRequest lastRequest = (DownloadFileRequest)sampleRequest.Clone();
            UInt64 start = lastRequest.Range.Start;

            //is set to true to start next circle
            Boolean isRightLastResponse = true;
            Boolean isWritenInFile = true;
            for (UInt64 end = maxChunkSize - 1;
                 (!IsFinishedDownload(start, end)) && (isRightLastResponse) && (isWritenInFile);
                 start = end + 1, end = ((end + maxChunkSize) < initialContantRange.TotalPerContact) ? (end + maxChunkSize) : initialContantRange.TotalPerContact - 1)
            {
                lastRequest.Range.Start = start;
                lastRequest.Range.End = end;

                (DownloadFileResponse response, RpcError rpcError) = await lastRequest.ResultAsyncWithCountDownloadedBytesUpdate(remoteContact, IOBehavior, discoveryService.ProtocolVersion).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                Boolean isRightResponse = IsRightDownloadFileResponse(lastRequest, response, rpcError);
                if (isRightResponse)
                {
                    if (fileStream.CanSeek)
                    {
                        //we use lock, because method fileStream.Write can be invoked from wrong position 
                        //(in case method seek is called by another thread)
                        lock (lockWriteFile)
                        {
                            fileStream.Seek(offset: (Int64)lastRequest.Range.Start, SeekOrigin.Begin);
                            
                            fileStream.Write(response.Chunk, offset: 0, response.Chunk.Length);
                            isWritenInFile = true;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot seek in file {lastRequest.FullPathToFile}");
                    }
                }
            }

            return lastRequest;
        }

        private ConcurrentDictionary<Contact, DownloadFileRequest> ContactsAndRequestsToDownload(DownloadFileRequest sampleRequest, IList<Contact> contactsWithFile, CancellationToken cancellationToken)
        {
            ConcurrentDictionary<Contact, DownloadFileRequest> contactsAndRequests = new ConcurrentDictionary<Contact, DownloadFileRequest>(comparer: new ContactComparer());

            UInt32 maxChunkSize = Constants.MaxChunkSize;

            UInt64 сountUndistributedBytes = sampleRequest.Range.Total;
            UInt64 lastPartBytesOfContact = 0;

            UInt32 partBytesOfContact = (UInt32)сountUndistributedBytes / (UInt32)contactsWithFile.Count;
            if (partBytesOfContact < maxChunkSize)
            {
                partBytesOfContact = maxChunkSize;
            }

            Int32 numChunk = 0;
            for (UInt32 numContact = 0;
                numContact < contactsWithFile.Count && сountUndistributedBytes > 0;
                numContact++, сountUndistributedBytes -= partBytesOfContact)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if ((сountUndistributedBytes < maxChunkSize) || (numContact == contactsWithFile.Count - 1))
                {
                    partBytesOfContact = (UInt32)сountUndistributedBytes;
                }

                DownloadFileRequest request = (DownloadFileRequest)sampleRequest.Clone();
                request.Range.Start = lastPartBytesOfContact;
                request.Range.TotalPerContact = partBytesOfContact;
                lastPartBytesOfContact = request.Range.Start + request.Range.TotalPerContact;

                request.Range.NumsUndownloadedChunk.AddRange(NumsChunk(request.Range.Start, lastPartBytesOfContact, maxChunkSize, ref numChunk));

                contactsAndRequests.TryAdd(contactsWithFile[(Int32)numContact], request);
            }

            return contactsAndRequests;
        }

        private List<Int32> NumsChunk(UInt64 start, UInt64 lastPartBytesOfContact, UInt32 maxChunkSize, ref Int32 lastNumChunk)
        {
            UInt64 chunk = start;
            var numsChunk = new List<Int32>();

            for (; chunk < lastPartBytesOfContact; chunk += maxChunkSize, lastNumChunk++)
            {
                numsChunk.Add(lastNumChunk);
            }

            return numsChunk;
        }

        private Boolean IsDownloadedFile(String fullPathToFile, UInt64 countOfBytes, IEnumerable<UInt64> downloadedBytesByEachContact)
        {
            Boolean isRightDownloaded;

            if (countOfBytes == (UInt64)downloadedBytesByEachContact.Sum(c => (Int64)c) && File.Exists(fullPathToFile))
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
        /// <see cref="Contact"/>s with <see cref="DownloadFileRequest"/>s with <see cref="Range"/>s which aren't download before
        /// </returns>
        private async Task<ConcurrentDictionary<Contact, DownloadFileRequest>> ContactsWithRequestToDownloadAsync(ConcurrentDictionary<Contact, DownloadFileRequest> contactsWithRequest, CancellationToken cancellationToken)
        {
            List<DownloadFileRequest> oldRequests = contactsWithRequest.Values.ToList();

            UInt64 сountUndistributedBytes = 0;
            foreach (var oldRequest in oldRequests)
            {
                if (oldRequest.Range.TotalPerContact >= oldRequest.CountDownloadedBytes)
                {
                    сountUndistributedBytes += oldRequest.Range.TotalPerContact - oldRequest.CountDownloadedBytes;
                }
                else
                {
                    throw new InvalidOperationException("Something was wrong during download process: " +
                        "too many bytes are downloaded from certain contact");
                }
            }

            ConcurrentDictionary<Contact, DownloadFileRequest> newContactsWithRequest = new ConcurrentDictionary<Contact, DownloadFileRequest>(new ContactComparer());
            UInt32 maxChunkSize = Constants.MaxChunkSize;
            var sampleRequest = oldRequests.First();

            List<Contact> contactsWithFile = await ContactsWithFileAsync(discoveryService.OnlineContacts, sampleRequest, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            for (Int32 numContact = 0, numRequest = 0;
                 (numRequest < oldRequests.Count) && (сountUndistributedBytes > 0);
                 сountUndistributedBytes -= oldRequests[numRequest].Range.TotalPerContact, numContact++, numRequest++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DownloadFileRequest request;
                Boolean wasDownloadedAllBytes;
                do
                {
                    request = oldRequests[numRequest];
                    request.Range.TotalPerContact -= request.CountDownloadedBytes;

                    wasDownloadedAllBytes = request.WasDownloadedAllBytes;
                    if (wasDownloadedAllBytes)
                    {
                        numRequest++;
                        request = oldRequests[numRequest];
                    }
                }
                while (wasDownloadedAllBytes);

                //maybe it should be call of method DownloadFileAsync in this if
                if ((сountUndistributedBytes - request.Range.TotalPerContact > 0) && (numContact == contactsWithFile.Count - 1))
                {
                    throw new FilePartiallyDownloadedException("Contacts have strange behavior. Cannot normally download file");
                }

                if (request.Range.TotalPerContact <= maxChunkSize)
                {
                    request.Range.End = request.Range.Start + request.Range.TotalPerContact;
                }

                newContactsWithRequest.TryAdd(contactsWithFile[numContact], request);
            }

            return newContactsWithRequest;
        }
    }
}
