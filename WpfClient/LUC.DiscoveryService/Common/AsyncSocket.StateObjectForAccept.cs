using System;
using System.Net.Sockets;
using System.Threading;

namespace LUC.DiscoveryServices.Common
{
    public partial class AsyncSocket : Socket
    {
        private sealed class StateObjectForAccept : IDisposable
        {
            public StateObjectForAccept( Socket listener, EventWaitHandle acceptDone )
            {
                Listener = listener;
                AcceptDone = acceptDone;
            }

            public Socket Listener { get; }

            public Socket AcceptedSocket { get; set; }

            public EventWaitHandle AcceptDone { get; }

            public void Dispose() =>
                AcceptDone.Dispose();
        }
    }
}
