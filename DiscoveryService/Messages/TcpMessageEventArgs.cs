using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    ///   The event data for <see cref="NetworkEventInvoker.QueryReceived"/> or
    ///   <see cref="NetworkEventInvoker.AnswerReceived"/>.
    /// </summary>
    public class TcpMessageEventArgs : MessageEventArgs
    {
        public Socket AcceptedSocket { get; set; }

        public EndPoint LocalEndPoint { get; set; }
    }
}
