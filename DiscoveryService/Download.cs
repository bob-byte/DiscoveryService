using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.DVVSet;
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

        private readonly DiscoveryService discoveryService;

        public Download(DiscoveryService discoveryService)
        {
            this.discoveryService = discoveryService;
        }

        public void DownloadFile(String localFolderPath, String localOriginalName, 
            String filePrefix, ObjectDescriptionModel fileDescription, String userId)
        {
            List<Contact> contactsWithFile = new List<Contact>();
            List<Contact> onlineContacts = discoveryService.KnownContacts.ToList();

            FileOperationTime operationTime = new FileOperationTime(fileDescription.OriginalName);
            Int32 maxChunkSize = Constants.MaxChunkSize;

            foreach (var contact in onlineContacts)
            {
                var isExistInContact = CheckFileExists(fileDescription, userId, operationTime.TimeStamp, contact);

                if (isExistInContact)
                {
                    contactsWithFile.Add(contact);
                }
            }

            if (contactsWithFile.Count >= 1)
            {
                //add cancellation of download
                if (fileDescription.Bytes <= maxChunkSize)
                {
                    ContantRange contantRange = new ContantRange
                    {
                        Start = 0,
                        End = fileDescription.Bytes - 1,
                        Total = fileDescription.Bytes
                    };
                    var request = new DownloadFileRequest
                    {
                        FileOriginalName = localOriginalName,
                        ContantRange = contantRange,
                        Prefix = filePrefix
                    };

                    //DownloadFileResponse downloadFileResponse = null;
                    //RpcError rpcError = null;
                    Int32 numContact;
                    Boolean isRightDownloaded = false;
                    for (numContact = 0; /*((rpcError == null) || (rpcError.HasError)) && */(numContact < contactsWithFile.Count) && (!isRightDownloaded); numContact++)
                    {
                        DownloadProcess(contactsWithFile[numContact], request, operationTime, 
                            filePrefix, localFolderPath, localOriginalName, userId);

                        isRightDownloaded = CheckDownloadedFile();
                    }
                }
                else
                {
                    //add a check for matching the number of contacts and the file size 
                    //divide evenly fileDescription.Bytes for contactsWithFile (take note vestigium of fileDescription.Bytes / contactsWithFile)
                    var vestigium = fileDescription.Bytes % contactsWithFile.Count;

                    Dictionary<Contact, DownloadFileRequest> contactsAndRequests = new Dictionary<Contact, DownloadFileRequest>(new ContactComparer());
                    var partBytesOfContact = fileDescription.Bytes % contactsWithFile.Count;
                    for (Int32 numContact = 0; numContact < contactsWithFile.Count; numContact++)
                    {
                        ContantRange contantRange = new ContantRange
                        {
                            Start = numContact * partBytesOfContact,
                            Total = fileDescription.Bytes,
                            TotalPerContact = partBytesOfContact
                        };
                        if (partBytesOfContact <= maxChunkSize)
                        {
                            contantRange.End = contantRange.Start + contantRange.TotalPerContact;
                        }

                        if (numContact == contactsWithFile.Count - 1)
                        {
                            contantRange.TotalPerContact += vestigium;
                        }

                        DownloadFileRequest request = new DownloadFileRequest
                        {
                            ContantRange = contantRange,
                            FileOriginalName = fileDescription.OriginalName
                        };

                        contactsAndRequests.Add(contactsWithFile[numContact], request);
                    }

                    //add redistribution if any contact changed file locally or now it is offline
                    Parallel.ForEach(contactsAndRequests, (contactAndRequest) =>
                    {
                        DownloadProcess(contactAndRequest.Key, contactAndRequest.Value, operationTime,
                            filePrefix, localFolderPath, localOriginalName, userId);
                    });
                }
            }
            else
            {
                throw new ArgumentException("This file doesn't exist in any node");
            }
        }

        private Boolean CheckFileExists(ObjectDescriptionModel fileDescription, String userId, 
            String timeStamp, Contact contact)
        {
            CheckFileExistsRequest request = new CheckFileExistsRequest
            {
                OriginalName = fileDescription.OriginalName,
                RandomID = ID.RandomID.Value,
                Sender = discoveryService.Service.OurContact.ID.Value
            };

            request.GetRequestResult(contact, out CheckFileExistsResponse response, out RpcError rpcError);
            
            VectorClock vectorClock = new VectorClock();
            String version = vectorClock.IncrementVersion(userId, timeStamp);

            Boolean isTheSameRequiredFile = (response.FileSize == fileDescription.Bytes) && (response.Version == version);
            Boolean existRequiredFile;
            if ((rpcError.HasError) && (response.Exist) && (isTheSameRequiredFile))
            {
                existRequiredFile = true;
            }
            else
            {
                existRequiredFile = false;
            }

            return existRequiredFile;
        }

        private void DownloadProcess(Contact remoteContact, DownloadFileRequest downloadFileRequest, FileOperationTime operationTime,
            String prefix, String localFolderPath, String localOriginalName, String userId)
        {
            Int32 maxChunkSize = Constants.MaxChunkSize;
            var initialContantRange = downloadFileRequest.ContantRange;
            var fullPath = Path.Combine(localFolderPath, localOriginalName);

            using(Stream fileStream = File.OpenWrite(fullPath))
            {
                if (initialContantRange.TotalPerContact <= maxChunkSize)
                {
                    downloadFileRequest.GetRequestResult<DownloadFileResponse>(remoteContact, out var response, out RpcError rpcError);

                    fileStream.Write(response.Buffer, offset: 0, response.Buffer.Length);
                }
                else
                {
                    Int64 start = 0;

                    for (Int64 end = maxChunkSize; end < initialContantRange.TotalPerContact;
                        end = ((end + maxChunkSize) < initialContantRange.TotalPerContact) ?
                        (end + maxChunkSize) : initialContantRange.TotalPerContact - 1)
                    {
                        ContantRange contantRange = new ContantRange(start, end, initialContantRange.Total);
                        var request = new DownloadFileRequest
                        {
                            ContantRange = contantRange,
                            FileOriginalName = localOriginalName,
                            Prefix = prefix,
                            Sender = downloadFileRequest.Sender
                        };

                        request.GetRequestResult<DownloadFileResponse>(remoteContact, out var response, out RpcError rpcError);
                        fileStream.Write(response.Buffer, offset: 0, count: response.Buffer.Length);

                        start = end;
                    }
                }
            }

        }

        private Boolean CheckDownloadedFile()
        {
            //compare version of files
        }

        //if contactWithFile != null then
        //get socket using ConnectionPool
        //send request to get file with fullPath
        //check available data the same as in ClienKadOperation
        //receive bytes
        //create file
        //return file

    }
}
