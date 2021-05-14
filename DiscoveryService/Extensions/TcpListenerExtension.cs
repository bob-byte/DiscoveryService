using LUC.DiscoveryService.Messages;
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
                
                //TODO add try...catch...finally
                var client = await receiver.AcceptTcpClientAsync();
                var stream = client.GetStream();
                TcpMessage message = new TcpMessage();
                message.Read(new CodingData.WireReader(stream));

                MessageEventArgs receiveResult = new MessageEventArgs();
                if (client.Client.RemoteEndPoint is IPEndPoint iPEndPoint)
                {
                    receiveResult.Message = message;
                    receiveResult.RemoteEndPoint = iPEndPoint;
                }

                stream.Close();
                client.Close();

                return receiveResult;
            });
        }
    }
}
