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
    class DownloadFileHandler : INetworkEventHandler
    {
        private readonly ISettingsService settingsService;

        public DownloadFileHandler(ICurrentUserProvider currentUserProvider)
        {
            settingsService = new SettingsService
            {
                CurrentUserProvider = currentUserProvider
            };
        }

        public void SendResponse(Object sender, TcpMessageEventArgs eventArgs)
        {
            DownloadFileRequest request = eventArgs.Message<DownloadFileRequest>(whetherReadMessage: false);

            if(request != null)
            {
                String rootFolderPath = settingsService.ReadUserRootFolderPath();
                String fullPathToFile = Path.Combine(rootFolderPath, request.Prefix, request.FileOriginalName);

                Byte[] fileBytes = File.ReadAllBytes(fullPathToFile);

                var response = new DownloadFileResponse
                {
                    RandomID = ID.RandomID.Value,
                    Buffer = fileBytes
                };
                response.Send(eventArgs.AcceptedSocket, request);
            }
        }
    }
}
