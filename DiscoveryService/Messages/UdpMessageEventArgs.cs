using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using System;
using System.Net;
using System.Numerics;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    ///   The event data for <see cref="NetworkEventHandler.QueryReceived"/> or
    ///   <see cref="NetworkEventHandler.AnswerReceived"/>.
    /// </summary>
    public class UdpMessageEventArgs : MessageEventArgs
    {
        //public UdpMessageEventArgs(EndPoint remoteEndPoint, BigInteger idOfSendingContact)
        //{
        //    RemoteEndPoint = remoteEndPoint;
        //    IdOfSendingContact = idOfSendingContact;
        //}

        /// <summary>
        ///   Message sender endpoint. It is used to store source IP address.
        /// </summary>
        /// <value>
        ///   The endpoint from the message was received.
        /// </value>
        public EndPoint RemoteEndPoint { get; set; }

        //public Byte[] Buffer { get; set; }

        //public BigInteger IdOfReceivingContact { get; set; }
    }
}
