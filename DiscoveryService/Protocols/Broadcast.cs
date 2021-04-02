using DiscoveryServices.Extensions.IPExtensions;
using System;
using System.Net;
using System.Net.Sockets;

namespace DiscoveryServices.Protocols
{
    class Broadcast
    {
        /// <summary>
        /// It sends a broadcast package
        /// </summary>
        /// <param name="ipAddressClass">
        /// Class of IP address where the package will be sent
        /// </param>
        /// <param name="subnetMask">
        /// It indicates how many peers will get this package
        /// </param>
        /// <param name="port">
        /// Port where the package will be sent
        /// </param>
        /// <param name="sendbuf">
        /// Buffer to send
        /// </param>
        public void Send(IPAddress ipAddressClass, IPAddress subnetMask, Int32 port, Byte[] sendbuf)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var ipBroadcast = ipAddressClass.GetBroadcastAddress(subnetMask);

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

        /// <summary>
        /// It listens to the UDP message
        /// </summary>
        /// 
        /// <param name="ipClass">
        /// Class of IP address wherefrom we should receive the message
        /// </param>
        /// 
        /// <param name="port">
        /// Port whence we should receive the message
        /// </param>
        /// 
        /// <param name="endPoint">
        /// Remote end point wherefrom we got the message
        /// </param>
        /// 
        /// <param name="bytes">
        /// Bytes which we get in message
        /// </param>
        public void Listen(IPAddress ipClass, Int32 port, out IPEndPoint endPoint, out Byte[] bytes)
        {
            UdpClient listener = new UdpClient(port);
            endPoint = new IPEndPoint(ipClass, port);

            try
            {
                bytes = listener.Receive(ref endPoint);
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
    }
}
