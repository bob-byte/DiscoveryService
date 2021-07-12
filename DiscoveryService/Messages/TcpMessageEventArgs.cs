using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using System;
using System.Net;
using System.Numerics;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    ///   The event data for <see cref="Service.QueryReceived"/> or
    ///   <see cref="Service.AnswerReceived"/>.
    /// </summary>
    public class TcpMessageEventArgs : MessageEventArgs
    {
        /// <summary>
        ///   Message sender endpoint. It is used to store source IP address.
        /// </summary>
        /// <value>
        ///   The endpoint from the message was received.
        /// </value>
        public EndPoint RemoteContact { get; set; }

        public BigInteger LocalContactId { get; set; }

        //public Contact LocalContact { get; set; }
    }
}
