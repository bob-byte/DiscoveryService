using LUC.DiscoveryService.Extensions.IPExtensions;
using System;
using System.Net;
using System.Net.Sockets;

namespace LUC.DiscoveryService.Protocols
{
    class Broadcast
    {
        private const Int32 PortForListening = 0;

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
            socket.EnableBroadcast = true;
            var ipBroadcast = ipAddressClass.GetBroadcastAddress(subnetMask);
            IPEndPoint ep = new IPEndPoint(ipBroadcast, port);

            try
            {
                //socket.Connect(ep);
                socket.SendTo(sendbuf, ep);
            }
            catch
            {
                throw;
            }
            finally
            {
                Close(SocketShutdown.Send, ref socket);
            }
        }

        private void Close(SocketShutdown shutdown, ref Socket socket)
        {
            socket.Shutdown(shutdown);
            socket.Close();
            socket = null;
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
        public void Listen(Byte[] bytes, IPEndPoint local_Ip, Int32 timeout)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.EnableBroadcast = true;
            var ipBroadcast = local_Ip.Address.GetBroadcastAddress(SubnetMask.ClassC);
            IPEndPoint localEndPoint = new IPEndPoint(ipBroadcast, local_Ip.Port);

            EndPoint remoteIp = new IPEndPoint(IPAddress.Any, PortForListening);
            socket.Bind(remoteIp);

            socket.ReceiveTimeout = timeout;

            try
            {
                //to enable receiving broadcast messages as I read. But i actually can't get such messages
                socket.SendTo(new Byte[] { 0, 2, 3 }, localEndPoint);
                socket.ReceiveFrom(bytes, ref remoteIp);
            }
            catch
            {
                throw;
            }
            finally
            {
                Close(SocketShutdown.Receive, ref socket);
            }
        }

    }
}
