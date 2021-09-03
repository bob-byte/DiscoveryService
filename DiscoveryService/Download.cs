using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Models;
using LUC.Services.Implementation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace LUC.DiscoveryService
{
    public class Download
    {
        class ContactComparer : IEqualityComparer<Contact>
        {
            public Boolean Equals(Contact contact1, Contact contact2)
            {
                Validate.IsTrue<ArgumentNullException>(contact1?.ID.Value != default, errorMessage: $"{nameof(contact1)} is equal to null");
                Validate.IsTrue<ArgumentNullException>(contact2?.ID.Value != default, errorMessage: $"{nameof(contact2)} is equal to null");

                var isEqual = contact1.ID == contact2.ID;
                return isEqual;
            }

            public Int32 GetHashCode(Contact contact)
            {
                Validate.IsTrue<ArgumentNullException>(contact?.ID.Value != default, errorMessage: $"{nameof(contact)} is equal to null");

                var hashCode = contact.ID.GetHashCode();
                return hashCode;
            }
        }

        private const String PostfixTempFile = "_temp";
        private const String MessIfThisFileDoesntExistInAnyNode = "This file doesn't exist in any node";

        private readonly Object lockWriteFile = new Object();
        private readonly DiscoveryService discoveryService;

        public Download(DiscoveryService discoveryService, IOBehavior ioBehavior)
        {
            this.discoveryService = discoveryService;
            LoggingService = AbstractService.LoggingService;
            IOBehavior = ioBehavior;
        }

        public IOBehavior IOBehavior { get; set; }

        public ILoggingService LoggingService { get; set; }

        public async Task DownloadFileAsync(String localFolderPath, String bucketName, String filePrefix, String localOriginalName,
            Int64 bytesCount, String fileVersion, CancellationToken cancellationToken)
        {
            if(cancellationToken != default)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            Boolean isRightParameters = IsRightInputParameters(localFolderPath, bucketName, filePrefix, 
                localOriginalName, bytesCount, fileVersion);
            if(isRightParameters)
            {
                List<Contact> onlineContacts = discoveryService.OnlineContacts.ToList();
                String fullPathToFile = FullPathToFile(onlineContacts, localFolderPath, bucketName, localOriginalName, filePrefix);
                //String localFileVersion = AdsExtensions.ReadLastSeenVersion(fullPathToFile);

                try
                {
                    //Boolean isNewFile = (!File.Exists(fullPathToFile))/* && (fileVersion != localFileVersion)*/;

                    if (/*(isNewFile) && */(onlineContacts.Count >= 1))
                    {
                        var initialRequest = new DownloadFileRequest
                        {
                            FullPathToFile = fullPathToFile,
                            FileOriginalName = localOriginalName,
                            BucketName = bucketName,
                            Range = new Range { Start = 0, Total = (UInt64)bytesCount },
                            FilePrefix = filePrefix,
                            FileVersion = fileVersion
                        };

                        List<Contact> contactsWithFile = await ContactsWithFileAsync(onlineContacts, initialRequest, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                        if (contactsWithFile.Count >= 1)
                        {
                            if (bytesCount <= Constants.MaxChunkSize)
                            {
                                await DownloadSmallFileAsync(contactsWithFile, initialRequest, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                await DownloadBigFileAsync(contactsWithFile, initialRequest, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            LoggingService.LogInfo(MessIfThisFileDoesntExistInAnyNode);
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    HandleException(ex, bytesCount, fullPathToFile);
                }
                catch (InvalidOperationException ex)
                {
                    HandleException(ex, bytesCount, fullPathToFile);
                }
            }
        }

        private void HandleException(Exception exception, Int64 bytesCount, String fullPathToFile)
        {
            LoggingService.LogInfo(exception.ToString());

            if (bytesCount > Constants.MaxChunkSize)
            {
                String tempFullPathToFile = TempFullFileName(fullPathToFile);
                TryDeleteFile(tempFullPathToFile);
            }
            else
            {
                TryDeleteFile(fullPathToFile);
            }
        }
        
        private Boolean IsRightInputParameters(String localFolderPath, String bucketName, String filePrefix, String localOriginalName,
            Int64 bytesCount, String fileVersion)
        {
            Boolean isRightInputParameters = (Directory.Exists(localFolderPath)) && 
                (bytesCount > 0) && (fileVersion != null);
            if (isRightInputParameters)
            {
                String pathToBucketName = Path.Combine(localFolderPath, bucketName);
                isRightInputParameters = Directory.Exists(pathToBucketName);

                if(isRightInputParameters)
                {
                    String pathFilePrefix = Path.Combine(localFolderPath, bucketName, filePrefix);
                    isRightInputParameters = Directory.Exists(pathFilePrefix);

                    if (isRightInputParameters)
                    {
                        String fullPathToFile = Path.Combine(pathFilePrefix, localFolderPath);
                        isRightInputParameters = !File.Exists(fullPathToFile); //file shouldn't exist before download
                    }
                }
            }

            return isRightInputParameters;
        }

        private void TryDeleteFile(String fullPathToFile)
        {            
            if(File.Exists(fullPathToFile))
            {
                File.Delete(fullPathToFile);
            }
        }

        private async Task<List<Contact>> ContactsWithFileAsync(IList<Contact> onlineContacts, DownloadFileRequest sampleRequest, CancellationToken cancellationToken)
        {
            var contactsWithFile = new List<Contact>();

            var checkFileExistsInContact = new ActionBlock<Contact>(async (contact) =>
            {
                var isExistInContact = await IsFileExistsInContactAsync(sampleRequest, contact).ConfigureAwait(continueOnCapturedContext: false);

                cancellationToken.ThrowIfCancellationRequested();

                if (isExistInContact)
                {
                    contactsWithFile.Add(contact);
                }
            });

            for (Int32 numContact = 0; numContact < onlineContacts.Count; numContact++)
            {
                checkFileExistsInContact.Post(onlineContacts[numContact]);
            }

            //Signals that we will not post more Contact
            checkFileExistsInContact.Complete();

            //await getting all contactsWithFile
            await checkFileExistsInContact.Completion.ConfigureAwait(false);

            return contactsWithFile;
        }

        private async Task<Boolean> IsFileExistsInContactAsync(DownloadFileRequest sampleRequest, Contact contact)
        {
            CheckFileExistsRequest request = new CheckFileExistsRequest
            {
                BucketName = sampleRequest.BucketName,
                FileOriginalName = sampleRequest.FileOriginalName,
                FilePrefix = sampleRequest.FilePrefix,
                Sender = discoveryService.NetworkEventInvoker.OurContact.ID.Value
            };
            (CheckFileExistsResponse response, RpcError rpcError) = await request.ResultAsync<CheckFileExistsResponse>(contact,
                IOBehavior, discoveryService.ProtocolVersion).ConfigureAwait(continueOnCapturedContext: false);

            Boolean existRequiredFile;
            if (!rpcError.HasError)
            {
                Boolean isTheSameRequiredFile = (response.FileSize == sampleRequest.Range.Total) && (response.FileVersion == sampleRequest.FileVersion);

                if ((response.FileExists) && (isTheSameRequiredFile))
                {
                    existRequiredFile = true;
                }
                else
                {
                    existRequiredFile = false;
                }
            }
            else
            {
                existRequiredFile = false;
            }

            return existRequiredFile;
        }

        private async Task DownloadSmallFileAsync(IList<Contact> contactsWithFile, DownloadFileRequest initialRequest, CancellationToken cancellationToken)
        {
            var request = (DownloadFileRequest)initialRequest.Clone();
            request.Range.End = request.Range.Total - 1;

            using (Stream fileStream = File.OpenWrite(request.FullPathToFile))
            {
                Boolean isRightDownloaded = false;
                RpcError rpcError = new RpcError();

                for (Int32 numContact = 0; (numContact < contactsWithFile.Count) && (!isRightDownloaded); numContact++)
                {
                    (isRightDownloaded, _) = await DownloadProcessAsync(contactsWithFile[numContact], request, fileStream,  cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

                    cancellationToken.ThrowIfCancellationRequested();

                    if((numContact == contactsWithFile.Count - 1) && (!isRightDownloaded))
                    {
                        TryDeleteFile(request.FullPathToFile);
                    }
                }
            }
        }

        /// <returns>
        /// If it is Functional test where is in use only current PC, return will be <paramref name="localFolderPath"/> + <paramref name="filePrefix"/> + <paramref name="localOriginalName"/>, else <paramref name="bucketName"/> also will be used
        /// </returns>
        private String FullPathToFile(ICollection<Contact> onlineContacts, String localFolderPath, 
            String bucketName, String localOriginalName, String filePrefix)
        {
            String fullPathToFile;
            Boolean canReceivedAnswerFromYourself = onlineContacts.Any(c => (discoveryService.ContactId == onlineContacts.First().ID));
            if (canReceivedAnswerFromYourself)
            {
                fullPathToFile = Path.Combine(localFolderPath, filePrefix, localOriginalName);
            }
            else
            {
                fullPathToFile = Path.Combine(localFolderPath, bucketName, filePrefix, localOriginalName);
            }

            return fullPathToFile;
        }

        /// <returns>
        /// First value returns whether <see cref="DownloadFileRequest.CountDownloadedBytes"/> is writen in <paramref name="fileStream"/>. The second returns <paramref name="downloadFileRequest"/> with updated <see cref="DownloadFileRequest.CountDownloadedBytes"/>, <paramref name="downloadFileRequest"/> will not be changed
        /// </returns>
        private async Task<(Boolean, DownloadFileRequest)> DownloadProcessAsync(Contact remoteContact, 
            DownloadFileRequest downloadFileRequest, Stream fileStream, CancellationToken cancellationToken)
        {
            UInt32 maxChunkSize = Constants.MaxChunkSize;
            Range initialContantRange = downloadFileRequest.Range;
            var isWritenInFile = false;
            DownloadFileRequest lastRequest = (DownloadFileRequest)downloadFileRequest.Clone();

            //initialContantRange.End(downloadFileRequest.Range) should be initialized outside this method in this case
            if (initialContantRange.TotalPerContact <= maxChunkSize)
            {
                (DownloadFileResponse response, RpcError rpcError) = await lastRequest.ResultAsyncWithCountDownloadedBytesUpdate
                    (remoteContact, IOBehavior, discoveryService.ProtocolVersion).ConfigureAwait(continueOnCapturedContext: false);

                cancellationToken.ThrowIfCancellationRequested();

                Boolean isRightResponse = IsRightDownloadFileResponse(lastRequest, response, rpcError);
                if (isRightResponse)
                {
                    fileStream.Write(response.Buffer, offset: 0, response.Buffer.Length);
                    isWritenInFile = true;
                }
            }
            else
            {
                UInt64 start = lastRequest.Range.Start;
                
                for (UInt64 end = maxChunkSize - 1; !IsFinishedDownload(start, end);
                    start = end + 1,
                    end = ((end + maxChunkSize) < initialContantRange.TotalPerContact) ?
                    (end + maxChunkSize) : initialContantRange.TotalPerContact - 1)
                {
                    lastRequest.Range.Start = start;
                    lastRequest.Range.End = end;

                    (DownloadFileResponse response, RpcError rpcError) = await lastRequest.ResultAsyncWithCountDownloadedBytesUpdate(remoteContact, IOBehavior, discoveryService.ProtocolVersion).ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();

                    Boolean isRightResponse = IsRightDownloadFileResponse(lastRequest, response, rpcError);
                    if (isRightResponse)
                    {
                        //we use lock, because method fileStream.Write can be invoked from wrong position 
                        //(in case method seek is called by another thread)
                        lock (lockWriteFile)
                        {
                            fileStream.Seek((Int64)start, SeekOrigin.Begin);
                            fileStream.Write(response.Buffer, offset: 0, response.Buffer.Length);
                        }

                        isWritenInFile = true;
                    }
                }
            }

            return (isWritenInFile, lastRequest);
        }

        private Boolean IsFinishedDownload(UInt64 start, UInt64 end) =>
            start >= end;

        private Boolean IsRightDownloadFileResponse(DownloadFileRequest request, DownloadFileResponse response, RpcError rpcError)
        {
            Boolean isReceivedRequiredRange = (!rpcError.HasError) && (response.IsRightBucket) && 
                (response.FileExists) && ((Int32)(request.Range.End - request.Range.Start) == response.Buffer.Length - 1);

            //file can be changed in remote contact during download process
            Boolean isTheSameFileInRemoteContact;
            if(isReceivedRequiredRange)
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
            ConcurrentDictionary<Contact, DownloadFileRequest> dictContactsWithRequest = ContactsAndRequestsToDownload(initialRequest, contactsWithFile, cancellationToken);

            //create temp file in order to another contacts don't download it
            String tempFullPath = TempFullFileName(initialRequest.FullPathToFile);

            Boolean isDownloadedFile = false;
            do
            {
                try
                {
                    //if file was renamed during download processes by user, it will be created again
                    using (Stream fileStream = File.OpenWrite(tempFullPath))
                    {
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
                            (_, DownloadFileRequest lastRequest) = await DownloadProcessAsync(contactWithRequest.Key,
                                contactWithRequest.Value, fileStream, cancellationToken)
                                .ConfigureAwait(continueOnCapturedContext: false);
                            cancellationToken.ThrowIfCancellationRequested();

                            //downloadProcess returns fixed request so we need to update contactsWithRequest
                            dictContactsWithRequest.AddOrUpdate(contactWithRequest.Key, lastRequest, (contact, oldRequest) => lastRequest);
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

                        isDownloadedFile = IsDownloadedFile(tempFullPath, (Int64)initialRequest.Range.Total, fileStream);
                        if (!isDownloadedFile)
                        {
                            //get new contacts and requests considering the previous download
                            dictContactsWithRequest = await ContactsWithRequestToDownloadAsync(dictContactsWithRequest,
                                cancellationToken).ConfigureAwait(false);

                            if (dictContactsWithRequest.Count == 0)
                            {
                                throw new InvalidOperationException(MessIfThisFileDoesntExistInAnyNode);
                            }
                        }
                    } //Writes all bytes in file only in the end of the using construction
                }
                catch(AggregateException ex)
                {
                    LoggingService.LogError(ex.ToString());
                }
            }
            while (!isDownloadedFile);

            if (!cancellationToken.IsCancellationRequested)
            {
                //rename file to requisite
                File.Move(tempFullPath, initialRequest.FullPathToFile);
            }
            else
            {
                File.Delete($"{initialRequest.FullPathToFile}{PostfixTempFile}");
            }
        }

        private ConcurrentDictionary<Contact, DownloadFileRequest> ContactsAndRequestsToDownload(DownloadFileRequest sampleRequest, IList<Contact> contactsWithFile, CancellationToken cancellationToken)
        {
            ConcurrentDictionary<Contact, DownloadFileRequest> contactsAndRequests = new ConcurrentDictionary<Contact, DownloadFileRequest>(new ContactComparer());

            UInt32 maxChunkSize = Constants.MaxChunkSize;

            UInt64 сountUndistributedBytes = sampleRequest.Range.Total;
            UInt64 lastPartBytesOfContact = 0;
            UInt32 partBytesOfContact;

            for (UInt32 numContact = 0;
                numContact < contactsWithFile.Count && сountUndistributedBytes > 0;
                numContact++, сountUndistributedBytes -= partBytesOfContact)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if ((сountUndistributedBytes < maxChunkSize) || (numContact == contactsWithFile.Count - 1))
                {
                    partBytesOfContact = (UInt32)сountUndistributedBytes;
                }
                else
                {
                    partBytesOfContact = maxChunkSize;
                }

                DownloadFileRequest request = (DownloadFileRequest)sampleRequest.Clone();
                request.Range.Start = lastPartBytesOfContact;
                request.Range.TotalPerContact = partBytesOfContact;
                lastPartBytesOfContact = request.Range.Start + request.Range.TotalPerContact;

                contactsAndRequests.TryAdd(contactsWithFile[(Int32)numContact], request);
            }

            return contactsAndRequests;
        }

        private String TempFullFileName(String fullPathToFile)
        {
            String tempFullPath;
            Boolean hasFileExt = Path.HasExtension(fullPathToFile);
            if (hasFileExt)
            {
                String ext = Path.GetExtension(fullPathToFile);
                String fullPathWithoutExt = fullPathToFile.TrimEnd(ext.ToCharArray());

                tempFullPath = $"{fullPathWithoutExt}{PostfixTempFile}{ext}";
            }
            else
            {
                tempFullPath = $"{fullPathToFile}{PostfixTempFile}";
            }

            while(File.Exists(tempFullPath))
            {
                tempFullPath += "_";
            }

            return tempFullPath;
        }

        private Boolean IsDownloadedFile(String fullPathToFile, Int64 countOfBytes, Stream fileStream)
        {
            Boolean isRightDownloaded;

            if ((countOfBytes == fileStream.Length) && File.Exists(fullPathToFile))
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
                if(oldRequest.Range.TotalPerContact >= oldRequest.CountDownloadedBytes)
                {
                    сountUndistributedBytes += oldRequest.Range.TotalPerContact - oldRequest.CountDownloadedBytes;
                }
                else
                {
                    throw new InvalidOperationException("Something was wrong during download process: too many bytes are downloaded from certain contact");
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

                DownloadFileRequest request = oldRequests[numRequest];
                request.Range.TotalPerContact -= request.CountDownloadedBytes;
                if (request.Range.TotalPerContact == 0)
                {
                    numRequest++;
                    request = oldRequests[numRequest];
                }

                //maybe it should be call of method DownloadFileAsync in this if
                if ((сountUndistributedBytes - oldRequests[numRequest].Range.TotalPerContact > 0) && (numContact == contactsWithFile.Count - 1))
                {
                    throw new InvalidOperationException("Contacts have strange behavior. Cannot normally download file");
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
