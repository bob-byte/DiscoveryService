using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
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
        public NetworkEventHandler( DiscoveryService discoveryService, NetworkEventInvoker networkEventInvoker, ICurrentUserProvider currentUserProvider )
        {
            INetworkEventHandler checkFileExistsHandler = new CheckFileExistsHandler( currentUserProvider, discoveryService );
            networkEventInvoker.CheckFileExistsReceived += checkFileExistsHandler.SendResponse;

            INetworkEventHandler downloadFileHandler = new DownloadFileHandler( currentUserProvider, discoveryService );
            networkEventInvoker.DownloadFileReceived += downloadFileHandler.SendResponse;

            Dht dht = NetworkEventInvoker.DistributedHashTable( discoveryService.ProtocolVersion );

            INetworkEventHandler kadOpRequestHandler = new KadOperationRequestHandler( dht );

            networkEventInvoker.PingReceived += kadOpRequestHandler.SendResponse;
            networkEventInvoker.StoreReceived += kadOpRequestHandler.SendResponse;
            networkEventInvoker.FindNodeReceived += kadOpRequestHandler.SendResponse;
            networkEventInvoker.FindValueReceived += kadOpRequestHandler.SendResponse;
        }
    }
}