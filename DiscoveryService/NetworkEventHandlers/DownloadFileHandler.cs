using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Interfaces.Helpers;
using LUC.Services.Implementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.NetworkEventHandlers
{
    class DownloadFileHandler : CheckFileExistsHandler
    {
        private static ISettingsService settingsService;
        private static ILoggingService loggingService;
        private static readonly Object lockReadFile = new Object();


        public DownloadFileHandler(ICurrentUserProvider currentUserProvider, DiscoveryService discoveryService)
            : base(currentUserProvider, discoveryService)
        {
            settingsService = new SettingsService
            {
                CurrentUserProvider = currentUserProvider
            };

            loggingService = new LoggingService
            {
                SettingsService = settingsService
            };
        }

        public override void SendResponse(Object sender, TcpMessageEventArgs eventArgs)
        {
            DownloadFileRequest request = eventArgs.Message<DownloadFileRequest>(whetherReadMessage: false);

            if(request != null)
            {
                try
                {
                    var checkFileResponse = FileResponse(request);
                    var downloadFileResponse = new DownloadFileResponse
                    {
                        FileExists = checkFileResponse.FileExists,
                        FileSize = checkFileResponse.FileSize,
                        FileVersion = checkFileResponse.FileVersion,
                        IsRightBucket = checkFileResponse.IsRightBucket,
                        RandomID = request.RandomID
                    };

                    if (downloadFileResponse.FileExists)
                    {
                        Byte[] fileBytes;

                        String rootFolderPath = settingsService.ReadUserRootFolderPath();
                        String fullPathToFile = Path.Combine(rootFolderPath, request.BucketName, request.FilePrefix, request.FileOriginalName);
                        if (request.Range.Total <= Constants.MaxChunkSize)
                        {
                            fileBytes = File.ReadAllBytes(fullPathToFile);
                        }
                        else
                        {
                            var countBytesToRead = (Int32)((request.Range.End + 1) - request.Range.Start);
                            fileBytes = new Byte[countBytesToRead];

                            //it is needed to use lock in case several contacts want to download the same file
                            lock (lockReadFile)
                            {
                                using (var stream = new FileStream(fullPathToFile, FileMode.Open, FileAccess.Read))
                                {
                                    stream./*BaseStream.*/Seek((Int64)request.Range.Start, origin: SeekOrigin.Begin);
                                    stream./*BaseStream.*/Read(fileBytes, offset: 0, countBytesToRead);
                                }
                            }                                
                        }

                        downloadFileResponse.Buffer = fileBytes;
                        downloadFileResponse.Send(request, eventArgs.AcceptedSocket);
                    }
                }
                catch(ArgumentNullException ex)
                {
                    loggingService.LogInfo(ex.ToString());
                }
                catch(IOException ex)
                {
                    loggingService.LogInfo(ex.ToString());
                }
                catch(Exception ex)
                {
                    loggingService.LogInfo(ex.ToString());
                }
            }
        }
    }
}
