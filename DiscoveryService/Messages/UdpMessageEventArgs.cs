using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages;

using System;
using System.Net;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages
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
