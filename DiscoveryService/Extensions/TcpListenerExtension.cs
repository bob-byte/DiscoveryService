using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Messages;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Extensions
{
    static class TcpListenerExtension
    {
        /// <summary>
        /// Equivalent to <see cref="UdpClient.ReceiveAsync"/>
        /// </summary>
        /// <returns>
        /// Task which return data of <see cref="TcpMessage"/> and remote IP address where from we received message
        /// </returns>
        public static Task<MessageEventArgs> ReceiveAsync(this TcpListener receiver)
        {
            return Task.Run(async () =>
            {
                var localEndpoint = receiver.LocalEndpoint as IPEndPoint;
                IPEndPoint iPEndPoint = null;
                TcpClient client = null;
                NetworkStream stream = null;
                TcpMessage message = new TcpMessage();

                try
                {
                    client = await receiver.AcceptTcpClientAsync();
                    stream = client.GetStream();
                    message.Read(new WireReader(stream));

                    iPEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                }
                catch
                {
                    throw;
                }
                finally
                {
                    stream?.Close();
                    client?.Close();
                }

                MessageEventArgs receiveResult = new MessageEventArgs();
                if (iPEndPoint != null)
                {
                    receiveResult.Message = message;
                    receiveResult.RemoteEndPoint = iPEndPoint;
                }
                else
                {
                    throw new InvalidOperationException("Cannot convert remote end point to IPEndPoint");
                }

                return receiveResult;
            });
        }
    }
}
