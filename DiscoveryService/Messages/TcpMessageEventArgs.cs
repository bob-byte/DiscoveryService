using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages
{
    /// <summary>
    ///   The event data for <see cref="NetworkEventInvoker.QueryReceived"/> or
    ///   <see cref="NetworkEventInvoker.AnswerReceived"/>.
    /// </summary>
    public class TcpMessageEventArgs : MessageEventArgs
    {
        /// <summary>
        /// Socket which is accepted after any <see cref="Socket.Accept"/> method
        /// </summary>
        public Socket AcceptedSocket { get; set; }

        /// <summary>
        /// <see cref="EndPoint"/> which received TCP message
        /// </summary>
        public EndPoint LocalEndPoint { get; set; }
    }
}
