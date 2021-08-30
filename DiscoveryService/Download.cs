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

namespace LUC.DiscoveryService
{
    public class Download
    {
        class ContactComparer : IEqualityComparer<Contact>
        {
            public Boolean Equals(Contact contact1, Contact contact2)
            {
                Validate.IsTrue<ArgumentNullException>(contact1?.ID != null, errorMessage: $"{nameof(contact1)} is equal to null");
                Validate.IsTrue<ArgumentNullException>(contact2?.ID != null, errorMessage: $"{nameof(contact2)} is equal to null");

                var isEqual = contact1.ID.Equals(contact2.ID);
                return isEqual;
            }

            public Int32 GetHashCode(Contact contact)
            {
                Validate.IsTrue<ArgumentNullException>(contact?.ID != null, errorMessage: $"{nameof(contact)} is equal to null");

                var hashCode = contact.ID.GetHashCode();
                return hashCode;
            }
        }

        private const String PostfixTempFile = "_temp";

        private readonly Object lockWriteFile = new Object();
        private readonly DiscoveryService discoveryService;

        public Download(DiscoveryService discoveryService)
        {
            this.discoveryService = discoveryService;
            LoggingService = AbstractService.LoggingService;
        }

        public ILoggingService LoggingService { get; set; }

        public async Task DownloadFileAsync(String localFolderPath, String localOriginalName, String filePrefix, 
            Int64 bytesCount, IOBehavior ioBehavior, CancellationToken cancellationToken)
        {
            List<Contact> onlineContacts = discoveryService.KnownContacts.ToList();
            List<Contact> contactsWithFile = await ContactsWithFile(onlineContacts, localOriginalName, filePrefix, bytesCount, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            UInt32 maxChunkSize = Constants.MaxChunkSize;

            if (contactsWithFile.Count >= 1)
            {
                if (bytesCount <= maxChunkSize)
                {
                    await DownloadSmallFile(contactsWithFile, localFolderPath, localOriginalName, filePrefix, 
                        bytesCount, ioBehavior, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await DownloadBigFile(contactsWithFile, localFolderPath, localOriginalName, filePrefix, 
                        bytesCount, ioBehavior, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                LoggingService.LogInfo("This file doesn't exist in any node");
            }
        }

        private async Task<List<Contact>> ContactsWithFile(List<Contact> onlineContacts, String localOriginalName, String filePrefix, Int64 bytesCount, IOBehavior ioBehavior, CancellationToken cancellationToken)
        {
            var contactsWithFile = new List<Contact>();

            foreach (var contact in onlineContacts)
            {
                var isExistInContact = await CheckFileExistsAsync(bytesCount, localOriginalName, filePrefix, contact, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                if (isExistInContact)
                {
                    contactsWithFile.Add(contact);
                }
            }

            return contactsWithFile;
        }

        private async Task<Boolean> CheckFileExistsAsync(Int64 bytesCount, String originalName, String filePrefix, Contact contact, IOBehavior ioBehavior)
        {
            CheckFileExistsRequest request = new CheckFileExistsRequest
            {
                OriginalName = originalName,
                FilePrefix = filePrefix,
                RandomID = ID.RandomID.Value,
                Sender = discoveryService.NetworkEventInvoker.OurContact.ID.Value
            };
            (CheckFileExistsResponse response, RpcError rpcError) = await request.ResultAsync<CheckFileExistsResponse>(contact,
                ioBehavior).ConfigureAwait(continueOnCapturedContext: false);

            String relativePath = Path.Combine(filePrefix, originalName);
            UInt64 version = (UInt64)AdsExtensions.ReadLastSeenModifiedUtc(relativePath);

            Boolean isTheSameRequiredFile = (response.FileSize == (UInt64)bytesCount) && (response.Version == version);
            Boolean existRequiredFile;

            if ((!rpcError.HasError) && (response.FileExists) && (isTheSameRequiredFile))
            {
                existRequiredFile = true;
            }
            else
            {
                existRequiredFile = false;
            }

            return existRequiredFile;
        }

        private async Task DownloadSmallFile(List<Contact> contactsWithFile, String localFolderPath, String localOriginalName, String filePrefix, Int64 bytesCount, IOBehavior ioBehavior, CancellationToken cancellationToken)
        {
            var contantRange = new Range
            {
                Start = 0,
                End = (UInt64)bytesCount - 1,
                Total = (UInt64)bytesCount
            };
            var request = new DownloadFileRequest
            {
                FileOriginalName = localOriginalName,
                ContantRange = contantRange,
                Prefix = filePrefix
            };

            String fullPathToFile = Path.Combine(localFolderPath, localOriginalName);
            using (Stream fileStream = File.OpenWrite(fullPathToFile))
            {
                Boolean isRightDownloaded = false;
                RpcError rpcError = new RpcError();

                for (Int32 numContact = 0; (numContact < contactsWithFile.Count) && (!isRightDownloaded); numContact++)
                {
                    (isRightDownloaded, _) = await DownloadProcess(contactsWithFile[numContact], request, fileStream, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
        }

        private async Task<(Boolean, DownloadFileRequest)> DownloadProcess(Contact remoteContact, 
            DownloadFileRequest downloadFileRequest, Stream fileStream, IOBehavior ioBehavior, CancellationToken cancellationToken)
        {
            UInt32 maxChunkSize = Constants.MaxChunkSize;
            Range initialContantRange = downloadFileRequest.ContantRange;
            var isWritenInFile = false;
            DownloadFileRequest lastRequest;

            if (initialContantRange.TotalPerContact <= maxChunkSize)
            {
                (DownloadFileResponse response, RpcError rpcError) = await downloadFileRequest.ResultAsync<DownloadFileResponse>
                    (remoteContact, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);
                lastRequest = downloadFileRequest;

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                Boolean isRightResponse = (Int32)(downloadFileRequest.ContantRange.End - downloadFileRequest.ContantRange.Start) ==
                    response.Buffer.Length;

                if (!rpcError.HasError && isRightResponse)
                {
                    fileStream.Write(response.Buffer, offset: 0, response.Buffer.Length);
                }
            }
            else
            {
                lastRequest = (DownloadFileRequest)downloadFileRequest.Clone();
                UInt64 start = lastRequest.ContantRange.Start;
                
                for (UInt64 end = maxChunkSize; end < initialContantRange.TotalPerContact;
                    end = ((end + maxChunkSize) < initialContantRange.TotalPerContact) ?
                    (end + maxChunkSize) : initialContantRange.TotalPerContact - 1)
                {
                    lastRequest.ContantRange.Start = start;
                    lastRequest.ContantRange.End = end;

                    (DownloadFileResponse response, RpcError rpcError) = await lastRequest.ResultAsync<DownloadFileResponse>(remoteContact).ConfigureAwait(false);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }

                    Boolean isRightResponse = (Int32)(end - start) == response.Buffer.Length;
                    if (!rpcError.HasError && isRightResponse)
                    {
                        //we use lock, because method write can be invoked from wrong position 
                        //(in case method seek is called by another thread)
                        lock (lockWriteFile)
                        {
                            fileStream.Seek((Int64)start, SeekOrigin.Begin);
                            fileStream.Write(response.Buffer, offset: 0, response.Buffer.Length);
                        }

                        isWritenInFile = true;
                    }

                    start = end;
                }
            }

            return (isWritenInFile, lastRequest);
        }

        private Boolean CheckDownloadedFile(String fullPathToFile, Int64 countOfBytes, Stream fileStream)
        {
            Boolean isRightDownloaded;

            if((countOfBytes == fileStream.Length) && File.Exists(fullPathToFile))
            {
                isRightDownloaded = true;
            }
            else
            {
                isRightDownloaded = false;
            }

            return isRightDownloaded;
        }

        private async Task DownloadBigFile(List<Contact> contactsWithFile, String localFolderPath, String localOriginalName, String filePrefix, Int64 bytesCount, IOBehavior ioBehavior, CancellationToken cancellationToken)
        {
            try
            {
                ConcurrentDictionary<Contact, DownloadFileRequest> contactsWithRequest = ContactsAndRequestsToDownload(localOriginalName, filePrefix, bytesCount, contactsWithFile, cancellationToken);
                
                String fullPathToFile = Path.Combine(localFolderPath, localOriginalName);

                Boolean isDownloadedFile = false;
                do
                {
                    //create temp file in order to another contacts don't download it
                    using (Stream fileStream = File.OpenWrite($"{fullPathToFile}{PostfixTempFile}"))
                    {
                        fileStream.SetLength(bytesCount);

                        ParallelOptions parallelOptions = new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Constants.MAX_THREADS,
                            CancellationToken = cancellationToken
                        };

                        //maybe it has a sense to use parameters with names localInit and localFinally
                        Parallel.ForEach(contactsWithRequest, parallelOptions, async (contactAndRequest, state) =>
                        {
                            (Boolean isWriten, DownloadFileRequest lastRequest) = await DownloadProcess(contactAndRequest.Key,
                                contactAndRequest.Value, fileStream, ioBehavior, cancellationToken)
                                .ConfigureAwait(continueOnCapturedContext: false);

                            contactsWithRequest.AddOrUpdate(contactAndRequest.Key, lastRequest, (contact, oldRequest) => lastRequest);
                        });

                        isDownloadedFile = CheckDownloadedFile(fullPathToFile, bytesCount, fileStream);
                    }

                    if (!isDownloadedFile)
                    {
                        //get new contacts and requests considering the previous download
                        contactsWithRequest = await ContactsAndRequestsToDownload(contactsWithRequest, ioBehavior,
                            cancellationToken).ConfigureAwait(false);

                        if(contactsWithRequest.Count == 0)
                        {
                            LoggingService.LogInfo("This file doesn't exist in any node");
                            return;
                        }
                    }
                }
                while (!isDownloadedFile);

                if (!cancellationToken.IsCancellationRequested)
                {
                    //rename file to requisite
                    File.Move($"{fullPathToFile}{PostfixTempFile}", fullPathToFile);
                }
                else
                {
                    File.Delete(fullPathToFile);
                }
            }
            catch(OperationCanceledException ex)
            {
                LoggingService.LogError(ex.ToString());
            }
        }

        private ConcurrentDictionary<Contact, DownloadFileRequest> ContactsAndRequestsToDownload(String localOriginalName, String filePrefix, Int64 bytesCount, ICollection<Contact> contactsWithFile, CancellationToken cancellationToken)
        {
            ConcurrentDictionary<Contact, DownloadFileRequest> contactsAndRequests = new ConcurrentDictionary<Contact, DownloadFileRequest>(new ContactComparer());
            List<Contact> listContactsWithFile = contactsWithFile.ToList();

            UInt32 maxChunkSize = Constants.MaxChunkSize;

            UInt64 сountUndistributedBytes = (UInt64)bytesCount;
            UInt64 lastPartBytesOfContact = 0;
            UInt32 partBytesOfContact;

            for (UInt32 numContact = 0;
                numContact < contactsWithFile.Count && сountUndistributedBytes > 0;
                numContact++, сountUndistributedBytes -= partBytesOfContact)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                if (сountUndistributedBytes < maxChunkSize)
                {
                    partBytesOfContact = (UInt32)сountUndistributedBytes;
                }
                else
                {
                    partBytesOfContact = maxChunkSize;
                }

                Range contantRange = new Range
                {
                    Start = lastPartBytesOfContact,
                    Total = (UInt64)bytesCount,
                    TotalPerContact = partBytesOfContact
                };

                contantRange.End = contantRange.Start + contantRange.TotalPerContact;
                lastPartBytesOfContact = contantRange.End;

                DownloadFileRequest request = new DownloadFileRequest
                {
                    ContantRange = contantRange,
                    FileOriginalName = localOriginalName,
                    Prefix = filePrefix
                };

                contactsAndRequests.TryAdd(listContactsWithFile[(Int32)numContact], request);
            }

            return contactsAndRequests;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localOriginalName"></param>
        /// <param name="filePrefix"></param>
        /// <param name="contactsWithRequests"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>
        /// <see cref="Contact"/>s with <see cref="DownloadFileRequest"/>s with <see cref="Range"/>s which aren't download before
        /// </returns>
        private async Task<ConcurrentDictionary<Contact, DownloadFileRequest>> ContactsAndRequestsToDownload(ConcurrentDictionary<Contact, DownloadFileRequest> contactsWithRequests, IOBehavior ioBehavior, CancellationToken cancellationToken)
        {
            List<DownloadFileRequest> oldRequests = contactsWithRequests.Values.ToList();
            
            UInt64 сountUndistributedBytes = 0;
            foreach (var oldRequest in oldRequests)
            {
                сountUndistributedBytes += oldRequest.ContantRange.TotalPerContact - oldRequest.CountDownloadedBytes;
            }
            UInt32 partBytesOfContact = 0;

            ConcurrentDictionary<Contact, DownloadFileRequest> newContactsWithRequests = new ConcurrentDictionary<Contact, DownloadFileRequest>(new ContactComparer());

            UInt32 maxChunkSize = Constants.MaxChunkSize;
            var sampleRequest = oldRequests.First();
            List<Contact> contactsWithFile = await ContactsWithFile(discoveryService.KnownContacts, sampleRequest.FileOriginalName,
                sampleRequest.Prefix, (Int64)sampleRequest.ContantRange.Total, ioBehavior, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            for (Int32 numContact = 0;
                numContact < contactsWithFile.Count && сountUndistributedBytes > 0;
                numContact++, сountUndistributedBytes -= partBytesOfContact)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                DownloadFileRequest request = oldRequests[numContact];
                request.ContantRange.TotalPerContact -= request.CountDownloadedBytes;
                if (request.ContantRange.TotalPerContact == 0)
                {
                    continue;
                }

                if (сountUndistributedBytes < maxChunkSize)
                {
                    partBytesOfContact = (UInt32)сountUndistributedBytes;
                }
                else
                {
                    partBytesOfContact = maxChunkSize;
                }

                if (request.ContantRange.TotalPerContact <= maxChunkSize)
                {
                    request.ContantRange.End = request.ContantRange.Start + request.ContantRange.TotalPerContact;
                }

                newContactsWithRequests.TryAdd(contactsWithFile[numContact], request);
            }

            return newContactsWithRequests;
        }
    }
}
