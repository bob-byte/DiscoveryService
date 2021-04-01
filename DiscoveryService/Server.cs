using DiscoveryServices.Protocols;
using System;
using System.Net;

namespace DiscoveryServices
{
    class Server
    {
        public void ListenBroadcast(IPAddress ipClass, Int32 port, out IPEndPoint endPoint, out byte[] bytes, out Peer peer)
        {
            try
            {
                Broadcast udpClient = new Broadcast();
                udpClient.Listen(ipClass, port, out endPoint, out bytes);

                peer = Parsing<Peer>.GetEncodedData(bytes);
            }
            catch
            {
                throw;
            }
        }

       




    }
}
