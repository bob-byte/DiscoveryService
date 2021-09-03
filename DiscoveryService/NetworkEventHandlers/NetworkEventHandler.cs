using LUC.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.NetworkEventHandlers
{
    class NetworkEventHandler
    {
        public NetworkEventHandler(DiscoveryService discoveryService, NetworkEventInvoker networkEventInvoker, ICurrentUserProvider currentUserProvider)
        {
            var checkFileExistsHandler = new CheckFileExistsHandler(currentUserProvider, discoveryService);
            networkEventInvoker.CheckFileExistsReceived += checkFileExistsHandler.SendResponse;
            
            var downloadFileHandler = new DownloadFileHandler(currentUserProvider, discoveryService);
            networkEventInvoker.DownloadFileReceived += downloadFileHandler.SendResponse;
        }
    }
}
