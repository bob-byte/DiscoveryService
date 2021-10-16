using System;
using System.Collections.Generic;
using System.Text;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    ///   The requested operation of a <see cref="DiscoveryServiceMessage"/>.
    /// </summary>
    /// <remarks>
    ///   Defines the standard operations.
    /// </remarks>
    /// <seealso cref="DiscoveryServiceMessage.Opcode"/>
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

        DownloadFile,
        DownloadFileResponse
    }
}
