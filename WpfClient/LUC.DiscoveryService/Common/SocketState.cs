namespace LUC.DiscoveryServices.Common
{
    public enum SocketState
    {
        Creating = 1,

        /// <summary>
        /// The socket has been created; no connection has been made
        /// </summary>
        Created = 2,

        Accepting = 4,

        Accepted = 8,

        /// <summary>
        /// The socket is attempting to connect to an another peer
        /// </summary>
        Connecting = 16,

        /// <summary>
        /// The socket is connected to a server; there is no active query
        /// </summary>
        Connected = 32,

        SendingBytes = 64,

        SentBytes = 128,

        Reading = 256,

        AlreadyRead = 512,

        Disconnecting = 1024,

        Disconnected = 2048,

        /// <summary>
        /// An unexpected error occurred; the socket is in an unusable state.
        /// </summary>
        Failed = 4096,

        /// <summary>
        /// The socket is closing
        /// </summary>
        Closing = 8192,

        /// <summary>
        /// The socked is closed
        /// </summary>
        Closed = 16384,
    }
}
