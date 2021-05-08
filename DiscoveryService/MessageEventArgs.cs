using LUC.DiscoveryService.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   The event data for <see cref="Service.QueryReceived"/> or
    ///   <see cref="Service.AnswerReceived"/>.
    /// </summary>
    public class MessageEventArgs : EventArgs
    {
        /// <summary>
        ///   The LightUpon.Cloud app message.
        /// </summary>
        /// <value>
        ///   The received message.
        /// </value>
        public Message Message { get; set; }

        /// <summary>
        ///   Message sender endpoint. It is used to store source IP address.
        /// </summary>
        /// <value>
        ///   The endpoint from the message was received.
        /// </value>
        public EndPoint RemoteEndPoint { get; set; }
    }
}
