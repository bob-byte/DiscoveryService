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

        public void DownloadFile(String localFolderPath, String localOriginalName, String filePrefix, UInt64 bytesCount, CancellationToken cancellationToken)
        {
            List<Contact> onlineContacts = discoveryService.KnownContacts.ToList();
            List<Contact> contactsWithFile = ContactsWithFile(onlineContacts, localOriginalName, filePrefix, bytesCount, cancellationToken);

            UInt32 maxChunkSize = Constants.MaxChunkSize;

            if (contactsWithFile.Count >= 1)
            {
                if (bytesCount <= maxChunkSize)
                {
                    DownloadSmallFile(contactsWithFile, localFolderPath, localOriginalName, filePrefix, bytesCount, cancellationToken);
                }
                else
                {
                    DownloadBigFile(contactsWithFile, localFolderPath, localOriginalName, filePrefix, bytesCount, cancellationToken);
                }
            }
            else
            {
                LoggingService.LogInfo("This file doesn't exist in any node");
            }
        }

        private List<Contact> ContactsWithFile(List<Contact> onlineContacts, String localOriginalName, String filePrefix, UInt64 bytesCount, CancellationToken cancellationToken)
        {
            var contactsWithFile = new List<Contact>();

            foreach (var contact in onlineContacts)
            {
                var isExistInContact = CheckFileExists(bytesCount, localOriginalName, filePrefix, contact);

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

        private Boolean CheckFileExists(UInt64 bytesCount, String originalName, String filePrefix, Contact contact)
        {
            CheckFileExistsRequest request = new CheckFileExistsRequest
            {
                OriginalName = originalName,
                FilePrefix = filePrefix,
                RandomID = ID.RandomID.Value,
                Sender = discoveryService.NetworkEventInvoker.OurContact.ID.Value
            };
            request.GetRequestResult(contact, out CheckFileExistsResponse response, out RpcError rpcError);
            
            UInt64 version = (UInt64)AdsExtensions.ReadLastSeenModifiedUtc(originalName);
            Boolean isTheSameRequiredFile = (response.FileSize == bytesCount) && (response.Version == version);
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

        private void DownloadSmallFile(List<Contact> contactsWithFile, String localFolderPath, String localOriginalName, String filePrefix, UInt64 bytesCount, CancellationToken cancellationToken)
        {
            Range contantRange = new Range
            {
                Start = 0,
                End = bytesCount - 1,
                Total = bytesCount
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
                    DownloadProcess(contactsWithFile[numContact], request, fileStream, cancellationToken, out isRightDownloaded, lastRequest: out _);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
        }

        private void DownloadProcess(Contact remoteContact, DownloadFileRequest downloadFileRequest, 
            Stream fileStream, CancellationToken cancellationToken, 
            out Boolean isWritenInFile, out DownloadFileRequest lastRequest)
        {
            UInt32 maxChunkSize = Constants.MaxChunkSize;
            var initialContantRange = downloadFileRequest.ContantRange;
            isWritenInFile = false;

            if (initialContantRange.TotalPerContact <= maxChunkSize)
            {
                downloadFileRequest.GetRequestResult<DownloadFileResponse>(remoteContact, out var response, out _);
                lastRequest = downloadFileRequest;

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                fileStream.Write(response.Buffer, offset: 0, response.Buffer.Length);
            }
            else
            {
                lastRequest = (DownloadFileRequest)downloadFileRequest.Clone();
                var start = lastRequest.ContantRange.Start;
                
                for (UInt64 end = maxChunkSize; end < initialContantRange.TotalPerContact;
                    end = ((end + maxChunkSize) < initialContantRange.TotalPerContact) ?
                    (end + maxChunkSize) : initialContantRange.TotalPerContact - 1)
                {
                    lastRequest.ContantRange.Start = start;
                    lastRequest.ContantRange.End = end;

                    lastRequest.GetRequestResult<DownloadFileResponse>(remoteContact, out var response, out RpcError rpcError);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
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

        private void DownloadBigFile(List<Contact> contactsWithFile, String localFolderPath, String localOriginalName, String filePrefix, UInt64 bytesCount, CancellationToken cancellationToken)
        {
            
            try
            {
                ConcurrentDictionary<Contact, DownloadFileRequest> contactsWithRequest = ContactsAndRequestsToDownload(localOriginalName, filePrefix, bytesCount, contactsWithFile, cancellationToken);
                
                String fullPathToFile = Path.Combine(localFolderPath, localOriginalName);

                Boolean isDownloadedFile = false;
                do
                {
                    WriteBigFile(contactsWithRequest, fullPathToFile, (Int64)bytesCount, cancellationToken, out isDownloadedFile);

                    if (!isDownloadedFile)
                    {
                        //get new contacts and requests considering the previous download
                        contactsWithRequest = ContactsAndRequestsToDownload(contactsWithRequest, cancellationToken);

                        if(contactsWithRequest.Count == 0)
                        {
                            LoggingService.LogInfo("This file doesn't exist in any node");
                            return;
                        }
                    }
                }
                while (!isDownloadedFile);

                if (cancellationToken.IsCancellationRequested)
                {
                    File.Delete(fullPathToFile);
                }
            }
            catch(OperationCanceledException ex)
            {
                LoggingService.LogError(ex.ToString());
            }
        }

        /// <summary>
        /// Also it adds to <paramref name="downloadedFileParts"/> 
        /// </summary>
        /// <param name="contactsAndRequests"></param>
        /// <param name="fullPathToFile"></param>
        /// <param name="bytesCount"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="downloadedFileParts"></param>
        private void WriteBigFile(ConcurrentDictionary<Contact, DownloadFileRequest> contactsAndRequests, String fullPathToFile, Int64 bytesCount, CancellationToken cancellationToken, out Boolean isDownloadedFIle)
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
                Parallel.ForEach(contactsAndRequests, parallelOptions, (contactAndRequest, state) =>
                {
                    DownloadProcess(contactAndRequest.Key, contactAndRequest.Value, fileStream, cancellationToken, 
                        out Boolean isWriten, out DownloadFileRequest lastRequest);
                    contactsAndRequests.AddOrUpdate(contactAndRequest.Key, lastRequest, (contact, oldRequest) => lastRequest);
                });

                isDownloadedFIle = CheckDownloadedFile(fullPathToFile, bytesCount, fileStream);
            }

            //rename file to requisite
            File.Move($"{fullPathToFile}{PostfixTempFile}", fullPathToFile);
        }

        private ConcurrentDictionary<Contact, DownloadFileRequest> ContactsAndRequestsToDownload(String localOriginalName, String filePrefix, UInt64 bytesCount, ICollection<Contact> contactsWithFile, CancellationToken cancellationToken)
        {
            ConcurrentDictionary<Contact, DownloadFileRequest> contactsAndRequests = new ConcurrentDictionary<Contact, DownloadFileRequest>(new ContactComparer());
            List<Contact> listContactsWithFile = contactsWithFile.ToList();

            UInt32 maxChunkSize = Constants.MaxChunkSize;

            UInt64 сountUndistributedBytes = bytesCount;
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
                    Total = bytesCount,
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
        private ConcurrentDictionary<Contact, DownloadFileRequest> ContactsAndRequestsToDownload(ConcurrentDictionary<Contact, DownloadFileRequest> contactsWithRequests, CancellationToken cancellationToken)
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
            List<Contact> contactsWithFile = ContactsWithFile(discoveryService.KnownContacts, sampleRequest.FileOriginalName, sampleRequest.Prefix, sampleRequest.ContantRange.Total, cancellationToken);

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
