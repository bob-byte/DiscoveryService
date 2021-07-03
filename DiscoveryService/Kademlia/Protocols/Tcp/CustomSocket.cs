using System;
using System.IO;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Net.Sockets;


namespace LUC.DiscoveryService.Kademlia.Protocols.Tcp
{
    public class CustomSocket:TcpClient
    {
        private DateTime _TimeCreated;

        public DateTime TimeCreated
        {
            get { return _TimeCreated; }
            set { _TimeCreated = value; }
        }

        public CustomSocket(string host,int port)
            : base(host,port)
        {
            _TimeCreated = DateTime.Now;
            
        }
    }
}
