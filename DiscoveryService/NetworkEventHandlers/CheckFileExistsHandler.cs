using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Interfaces.Extensions;
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
    class CheckFileExistsHandler : INetworkEventHandler
    {
        private readonly ISettingsService settingsService;
        private readonly DiscoveryService discoveryService;

        public CheckFileExistsHandler(ICurrentUserProvider currentUserProvider, DiscoveryService discoveryService)
        {
            this.discoveryService = discoveryService;
            settingsService = new SettingsService
            {
                CurrentUserProvider = currentUserProvider
            };
        }

        public virtual void SendResponse(Object sender, TcpMessageEventArgs eventArgs)
        {
            var request = eventArgs.Message<CheckFileExistsRequest>(whetherReadMessage: false);
            var response = FileResponse(request);
            
            response.Send(request, eventArgs.AcceptedSocket);
        }

        protected FileResponse FileResponse(FileRequest request)
        {
            var response = new CheckFileExistsResponse
            {
                IsRightBucket = discoveryService.GroupsSupported.ContainsKey(request.BucketName),
                RandomID = request.RandomID
            };
            String rootFolderPath = settingsService.ReadUserRootFolderPath();
            String fullPath = Path.Combine(rootFolderPath, request.BucketName, request.FilePrefix, request.FileOriginalName);

            response.FileExists = File.Exists(fullPath);

            if (response.IsRightBucket)
            {
                
                if (response.FileExists)
                {
                    var fileInfo = FileInfoHelper.TryGetFileInfo(fullPath);
                    response.FileSize = (UInt64)fileInfo.Length;

                    response.FileVersion = AdsExtensions.ReadLastSeenVersion(fullPath);
                }
            }

            return response;
        }
    }
}
