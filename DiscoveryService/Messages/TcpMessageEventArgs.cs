using System;
using System.Net;
using System.Net.Sockets;

namespace DiscoveryServices.Messages
{
    /// <summary>
    ///   The event data for <see cref="NetworkEventInvoker.QueryReceived"/> or
    ///   <see cref="NetworkEventInvoker.AnswerReceived"/>.
    /// </summary>
    public class TcpMessageEventArgs : MessageEventArgs
    {
        //public TcpMessageEventArgs(Byte[] buffer, Socket acceptedSocket, Action unregisterSocket, EndPoint localEndPoint, EndPoint remoteEndPoint )
        //{ }

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
