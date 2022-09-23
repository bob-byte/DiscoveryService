using System;
using System.Net;
using System.Net.Sockets;

namespace LUC.DiscoveryServices.Messages
{
    /// <summary>
    ///   The event data for TCP message receiving.
    /// </summary>
    public class TcpMessageEventArgs : MessageEventArgs
    {
        /// <summary>
        /// Socket which is accepted after any <see cref="Socket.Accept"/> method
        /// </summary>
        public Socket AcceptedSocket { get; set; }

        /// <summary>
        /// Use it when you receive malformed message
        /// </summary>
        internal Action UnregisterSocket { get; set; }

        /// <summary>
        /// <see cref="EndPoint"/> which received TCP message
        /// </summary>
        public EndPoint LocalEndPoint { get; set; }
    }
}
