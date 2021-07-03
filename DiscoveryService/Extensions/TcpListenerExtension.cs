using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.Protocols.Tcp;
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
        public static Task<TcpMessageEventArgs> ReceiveAsync<T>(this TcpListener receiver, Contact receivingContact = null)
            where T: TcpMessage, new()
        {


            return Task.Run(async () =>
            {
                var localEndpoint = receiver.LocalEndpoint as IPEndPoint;
                IPEndPoint iPEndPoint = null;
                TcpClient client = null;
                NetworkStream stream = null;
                T message = new T();

                try
                {
                    client = await receiver.AcceptTcpClientAsync();
                    stream = client.GetStream();
                    message.Read(new WireReader(stream));

                    iPEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                }
                finally
                {
                    client?.Close();
                    stream?.Close();
                }

                TcpMessageEventArgs receiveResult = new TcpMessageEventArgs();
                if (iPEndPoint != null)
                {
                    receiveResult.Message = message;
                    receiveResult.RemoteContact = new Contact(new TcpProtocol(), new ID(message.IdOfSendingContact), iPEndPoint.Address, message.TcpPort);
                    receiveResult.LocalContact = receivingContact;
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
