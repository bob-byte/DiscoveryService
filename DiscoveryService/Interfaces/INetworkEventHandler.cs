using DiscoveryServices.Messages;

using System;

namespace DiscoveryServices.Interfaces
{
    interface INetworkEventHandler
    {
        void SendResponse( Object sender, TcpMessageEventArgs eventArgs );
    }
}
