using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Common
{
    public enum SocketState
    {
        /// <summary>
        /// The socket has been created; no connection has been made
        /// </summary>
        Created = 1,

        /// <summary>
        /// The socket is attempting to connect to an another peer
        /// </summary>
        Connecting = 2,

        /// <summary>
        /// The socket is connected to a server; there is no active query
        /// </summary>
        Connected = 4,

        SendingBytes = 8,

        SentBytes = 16,

        Reading = 32,

        AlreadyRead = 64,

        Disconnected = 128,

        /// <summary>
        /// The socket is closing
        /// </summary>
        Closing = 256,

        /// <summary>
        /// The socked is closed
        /// </summary>
        Closed = 512,

        /// <summary>
        /// An unexpected error occurred; the socket is in an unusable state.
        /// </summary>
        Failed = 1024
    }
}
