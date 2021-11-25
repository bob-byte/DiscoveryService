using LUC.DiscoveryServices.Messages;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Interfaces
{
    interface INetworkEventHandler
    {
        void SendResponse( Object sender, TcpMessageEventArgs eventArgs );
    }
}
