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
        /// <summary>
        ///   Acknowledge operation is type of TCP packet, sent on response to UDP message.
        /// </summary>
        Acknowledge = 0,

        Ping = 1,
        PingResponse = 2,
        Store = 3,
        StoreResponse = 4,
        FindNode = 5,
        FindNodeResponse = 6,
        FindValueResponseWithValue = 7,
        FindValueResponseWithCloseContacts = 8
    }
}
