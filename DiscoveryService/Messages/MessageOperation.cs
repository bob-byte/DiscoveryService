using System;

namespace DiscoveryServices.Messages
{
    /// <summary>
    ///   The requested operation of a <see cref="DiscoveryMessage"/>.
    /// </summary>
    /// <remarks>
    ///   Defines the standard operations.
    /// </remarks>
    /// <seealso cref="DiscoveryMessage.Opcode"/>
    public enum MessageOperation : Byte
    {
        LocalError,

        Multicast,

        /// <summary>
        ///   Acknowledge operation is type of TCP packet, sent on response to UDP message.
        /// </summary>
        Acknowledge,

        Ping,
        PingResponse,

        Store,
        StoreResponse,

        FindNode,
        FindNodeResponse,

        FindValue,
        FindValueResponseWithValue,
        FindValueResponseWithCloseContacts,

        CheckFileExists,
        CheckFileExistsResponse,

        DownloadChunk,
        DownloadChunkResponse
    }
}
