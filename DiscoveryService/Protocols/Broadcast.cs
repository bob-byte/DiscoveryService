using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace DiscoveryServices.Protocols
{
    class Broadcast
    {
        public Int32 Port { get; set; }

        public Broadcast(Int32 port)
        {
            Port = port;
        }

        public Broadcast()
        {
            DoNothing();
        }

        //inline-function
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoNothing()
        {
            ;
        }

        public void Listen(out IPEndPoint groupEndPoint)
        {
            UdpClient listener = new UdpClient(Port);
            groupEndPoint = new IPEndPoint(IPAddress.Any, Port);

            try
            {
                var bytes = listener.Receive(ref groupEndPoint);
                Console.WriteLine(Encoding.ASCII.GetString(bytes));
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

        public void Listen(out IPEndPoint groupEndPoint, out byte[] bytes)
        {
            UdpClient listener = new UdpClient(Port);
            groupEndPoint = new IPEndPoint(IPAddress.Any, Port);
            bytes = null;

            try
            {
                while (true)
                {
                    bytes = listener.Receive(ref groupEndPoint);
                }
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

        public void SendBroadcastToAll(Byte[] sendbuf, String ipNetwork)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            var bytes = IPAddress.Parse(ipNetwork).GetAddressBytes();
            bytes[3] = 255;
            var broadcast = new IPAddress(bytes);

            IPEndPoint ep = new IPEndPoint(broadcast, Port);

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
