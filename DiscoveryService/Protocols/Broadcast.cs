using DiscoveryServices.Extensions.IPExtensions;
using System;
using System.Net;
using System.Net.Sockets;

namespace DiscoveryServices.Protocols
{
    class Broadcast
    {
        public void Listen(IPAddress ipClass, Int32 port, out IPEndPoint groupEndPoint, out Byte[] bytes)
        {
            UdpClient listener = new UdpClient(port);
            groupEndPoint = new IPEndPoint(ipClass, port);

            try
            {
                bytes = listener.Receive(ref groupEndPoint);
            }
            catch
            {
                throw;
            }
            finally
            {
                listener.Close();
            }
        }

        public void Send(IPAddress ipNetwork, IPAddress subnetMask, Int32 port, Byte[] sendbuf)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var ipBroadcast = ipNetwork.GetBroadcastAddress(subnetMask);

            IPEndPoint ep = new IPEndPoint(ipBroadcast, port);

            try
            {
                socket.SendTo(sendbuf, ep);
            }
            catch
            {
                throw;
            }
            finally
            {
                socket.Close();
            }
        }
    }
}
