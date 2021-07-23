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
    ///   The event data for <see cref="NetworkEventHandler.QueryReceived"/> or
    ///   <see cref="NetworkEventHandler.AnswerReceived"/>.
    /// </summary>
    public class TcpMessageEventArgs : MessageEventArgs
    {
        /// <summary>
        ///   Message sender endpoint. It is used to store source IP address.
        /// </summary>
        /// <value>
        ///   The endpoint from the message was received.
        /// </value>
        public EndPoint SendingEndPoint { get; set; }

        public Socket AcceptedSocket { get; set; }

        public EndPoint LocalEndPoint { get; set; }


        //public IEnumerable<EndPoint> RemoteEndPoints { get; set; }

        //public Contact LocalContact { get; set; }
    }
}
