using LUC.DiscoveryServices.Messages;

using System;

namespace LUC.DiscoveryServices.Common.Interfaces
{
    interface INetworkEventHandler
    {
        void SendResponse( Object sender, TcpMessageEventArgs eventArgs );
    }
}
