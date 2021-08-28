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
        public NetworkEventHandler(NetworkEventInvoker networkEventInvoker, ICurrentUserProvider currentUserProvider)
        {
            var checkFileExistsHandler = new CheckFileExistsHandler(currentUserProvider);
            networkEventInvoker.CheckFileExistsReceived += checkFileExistsHandler.SendResponse;
            
            var downloadFileHandler = new DownloadFileHandler(currentUserProvider);
            networkEventInvoker.DownloadFileReceived += downloadFileHandler.SendResponse;
        }
    }
}
