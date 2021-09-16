using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;

using System;
using System.Net;
using System.Numerics;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    ///   The event data for <see cref="NetworkEventInvoker.QueryReceived"/> or
    ///   <see cref="NetworkEventInvoker.AnswerReceived"/>.
    /// </summary>
    public class UdpMessageEventArgs : MessageEventArgs
    {
        //Current class is used for better concomitance and readability.
        //Now it contains nothing, because MessageEventArgs has properties enough.
    }
}
