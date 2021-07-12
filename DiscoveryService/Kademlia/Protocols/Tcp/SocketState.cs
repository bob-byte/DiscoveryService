using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia.Protocols.Tcp
{
    enum SocketState
    {
        /// <summary>
        /// The socket has been created; no connection has been made
        /// </summary>
        Created,

        /// <summary>
        /// The socket is attempting to connect to an another peer
        /// </summary>
        Connecting,

        /// <summary>
        /// The socket is connected to a server; there is no active query
        /// </summary>
        Connected,

        /// <summary>
        /// The socked is connected to an another peer and a query is being made.
        /// </summary>
        Querying,


        /// <summary>
        /// The socked is connected to an another peer and the active query is being cancelled.
        /// </summary>
        CancelingQuery,

        /// <summary>
        /// A cancellation is pending on the connected peer and needs to be cleared
        /// </summary>
        ClearingPendingCancellation,

        /// <summary>
        /// The socket is closing
        /// </summary>
        Closing,

        /// <summary>
        /// The socked is closed
        /// </summary>
        Closed,

        /// <summary>
        /// An unexpected error occurred; the socket is in an unusable state.
        /// </summary>
        Failed
    }
}
