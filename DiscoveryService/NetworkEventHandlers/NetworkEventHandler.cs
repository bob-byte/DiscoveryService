using DiscoveryServices.Interfaces;
using DiscoveryServices.Kademlia;
using LUC.Interfaces;

namespace DiscoveryServices.NetworkEventHandlers
{
    class NetworkEventHandler
    {
        public NetworkEventHandler( DiscoveryService discoveryService, NetworkEventInvoker networkEventInvoker, ICurrentUserProvider currentUserProvider )
        {
            INetworkEventHandler checkFileExistsHandler = new CheckFileExistsRequestHandler( currentUserProvider, discoveryService );
            networkEventInvoker.CheckFileExistsReceived += checkFileExistsHandler.SendResponse;

            INetworkEventHandler downloadFileHandler = new DownloadChunkRequestHandler( currentUserProvider, discoveryService );
            networkEventInvoker.DownloadFileReceived += downloadFileHandler.SendResponse;

            Dht dht = NetworkEventInvoker.DistributedHashTable( discoveryService.ProtocolVersion );

            INetworkEventHandler kadOpRequestHandler = new KadRpcHandler( dht, discoveryService.ProtocolVersion );

            networkEventInvoker.PingReceived += kadOpRequestHandler.SendResponse;
            networkEventInvoker.StoreReceived += kadOpRequestHandler.SendResponse;
            networkEventInvoker.FindNodeReceived += kadOpRequestHandler.SendResponse;
            networkEventInvoker.FindValueReceived += kadOpRequestHandler.SendResponse;
        }
    }
}