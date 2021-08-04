using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService
{
    class StateObjectForAccept
    {
        public StateObjectForAccept(Socket listener)
        {
            Listener = listener;
        }

        public Socket Listener { get; set; }

        public Socket AcceptedSocket { get; set; }
    }
}
