using System;

namespace LUC.DiscoveryServices.Common
{
    public enum SocketState : Byte
    {
        /// <summary>
        /// The socket has been created; no connection has been made
        /// </summary>
        Created,

        Accepting,

        Accepted,

        /// <summary>
        /// The socket is attempting to connect to an another peer
        /// </summary>
        Connecting,

        /// <summary>
        /// The socket is connected to a server; there is no active query
        /// </summary>
        Connected,

        SendingBytes,

        SentBytes,

        Reading,

        AlreadyRead,

        Disconnecting,

        Disconnected,

        /// <summary>
        /// An unexpected error occurred; the socket is in an unusable state.
        /// </summary>
        Failed,

        /// <summary>
        /// The socket is closing
        /// </summary>
        Closing,

        /// <summary>
        /// The socked is closed
        /// </summary>
        Closed,
    }
}
