using DiscoveryServices.Protocols;
using System;
using System.Net;

namespace DiscoveryServices
{
    class Server
    {
        /// <summary>
        /// It listens to a broadcast message and returns bytes 
        /// which we get in the last one and info of peer wherefrom we has received it
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
        /// 
        /// <param name="peer">
        /// Peer whence we get message
        /// </param>
        public void ListenBroadcast(IPAddress ipClass, Int32 port, out IPEndPoint endPoint, out Byte[] bytes, out Peer peer)
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
