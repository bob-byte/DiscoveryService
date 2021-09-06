using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.DVVSet;
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
    public partial class Download
    {
        private const String MessIfThisFileDoesntExistInAnyNode = "This file doesn't exist in any node";

        private readonly Object lockWriteFile;
        private readonly DownloadedFile downloadedFile;
        private readonly DiscoveryService discoveryService;

        public Download(DiscoveryService discoveryService, IOBehavior ioBehavior)
        {
            downloadedFile = new DownloadedFile();
            lockWriteFile = new Object();

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
                String fullPathToFile = downloadedFile.FullPathToFile(onlineContacts, discoveryService.MachineId, 
                    localFolderPath, bucketName, localOriginalName, filePrefix);

                try
                {
                    if (onlineContacts.Count >= 1)
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
                catch (PathTooLongException ex)
                {
                    HandleException(ex, bytesCount, fullPathToFile);
                }
                catch (ArgumentException ex)
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
                downloadedFile.TryDeleteFile(fullPathToFile);

                String tempFullPathToFile = downloadedFile.TempFullFileName(fullPathToFile);
                downloadedFile.TryDeleteFile(tempFullPathToFile);
            }
            else
            {
                downloadedFile.TryDeleteFile(fullPathToFile);
            }
        }

        private Boolean IsRightInputParameters(String localFolderPath, String bucketName, String filePrefix, String localOriginalName,
            Int64 bytesCount, String fileVersion)
        {
            Boolean isRightInputParameters = (Directory.Exists(localFolderPath)) && 
                (bytesCount > 0) && (fileVersion != null);//check first 4 parameters for null is in system methods
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
                        //file shouldn't exist before download
                        String fullPathToFile = downloadedFile.FullPathToFile(
                            discoveryService.OnlineContacts, 
                            discoveryService.MachineId, 
                            localFolderPath, 
                            bucketName, 
                            localOriginalName, 
                            filePrefix);

                        isRightInputParameters = !File.Exists(fullPathToFile);

                        if(!isRightInputParameters)
                        {
                            LoggingService.LogInfo($"File {fullPathToFile} already exists");
                        }
                    }
                }
            }

            return isRightInputParameters;
        }
    }
}
