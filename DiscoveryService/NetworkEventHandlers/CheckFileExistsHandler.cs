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

        public CheckFileExistsHandler(ICurrentUserProvider currentUserProvider)
        {
            settingsService = new SettingsService
            {
                CurrentUserProvider = currentUserProvider
            };
        }

        public void SendResponse(Object sender, TcpMessageEventArgs eventArgs)
        {
            var request = eventArgs.Message<CheckFileExistsRequest>(whetherReadMessage: false);

            var response = new CheckFileExistsResponse();
            response.IsRightBucket = DiscoveryService.GroupsSupported.ContainsKey(request.BucketName);

            if (response.IsRightBucket)
            {
                String rootFolderPath = settingsService.ReadUserRootFolderPath();
                String fullPath = Path.Combine(rootFolderPath, request.FilePrefix, request.OriginalName);

                response.FileExists = File.Exists(fullPath);
                if (response.FileExists)
                {
                    var fileInfo = FileInfoHelper.TryGetFileInfo(fullPath);
                    response.FileSize = (UInt64)fileInfo.Length;

                    response.Version = (UInt64)AdsExtensions.ReadLastSeenModifiedUtc(fullPath);
                }
            }
            response.Send(request, eventArgs.AcceptedSocket, Constants.SendTimeout);
        }
    }
}
