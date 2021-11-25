using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Common
{
    class StateObjectForAccept : IDisposable
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
