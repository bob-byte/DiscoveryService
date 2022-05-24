using System;
using System.Net.Sockets;
using System.Threading;

namespace LUC.DiscoveryServices.Common
{
    public sealed class StateObjectForAccept : IDisposable
    {
        public StateObjectForAccept( Socket listener, EventWaitHandle acceptDone )
        {
            Listener = listener;
            AcceptDone = acceptDone;
        }

        public Socket Listener { get; set; }

        public Socket AcceptedSocket { get; set; }

        public EventWaitHandle AcceptDone { get; }

        public void Dispose() =>
            AcceptDone.Dispose();
    }
}
