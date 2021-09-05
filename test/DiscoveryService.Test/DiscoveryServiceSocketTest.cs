using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Test
{
    [TestFixture]
    class DiscoveryServiceSocketTest
    {
        [Test]
        public void Connect_RemoteEndPointIsNull_GetException()
        {
            var dsSocket = InitializedTcpSocket();

            Assert.That(() => 
                    dsSocket.Connect(remoteEndPoint: null, TimeSpan.FromSeconds(1)), 
                    Throws.TypeOf(typeof(ArgumentNullException)));
        }

        [Test]
        public void ConnectAsync_RemoteEndPointIsNull_GetException()
        {
            var dsSocket = InitializedTcpSocket();

            Assert.That(async () =>
                    await dsSocket.ConnectAsync(remoteEndPoint: null, TimeSpan.FromSeconds(1)),
                    Throws.TypeOf(typeof(ArgumentNullException)));
        }

        [Test]
        public void Receive_SendPingRequestAndReceiveResponse_NotFailed()
        {
            DiscoveryService discoveryService = new DiscoveryService(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1));
            discoveryService.Start();

            var client = InitializedTcpSocket();

            var endPoint = AvailableIpAddress(discoveryService, client.AddressFamily);
            client.Connect(endPoint, Constants.ConnectTimeout);

            var pingRequest = new PingRequest
            {
                Sender = ID.RandomIDInKeySpace.Value
            };

            client.Send(pingRequest.ToByteArray(), Constants.SendTimeout);

            Thread.Sleep(TimeSpan.FromSeconds(value: 5));
            if(client.Available > 0)
            {
                var receivedBytes = client.Receive(timeout: default);

                Assert.IsTrue(receivedBytes.Length > 0);
            }
            else
            {
                throw new TimeoutException($"Timeout to receive {typeof(PingResponse).Name} data");
            }
        }

        private DiscoveryServiceSocket InitializedTcpSocket() =>
            new DiscoveryServiceSocket(
                AddressFamily.InterNetwork, 
                SocketType.Stream, 
                ProtocolType.Tcp, 
                SetUpTests.LoggingService
            );

        private IPEndPoint AvailableIpAddress(DiscoveryService discoveryService, AddressFamily addressFamily)
        {
            var allPossibleIpAddresses = discoveryService.NetworkEventInvoker.RunningIpAddresses.Where(c => c.AddressFamily == addressFamily).ToArray();

            Random random = new Random();
            var ipAddress = allPossibleIpAddresses[1/*random.Next(allPossibleIpAddresses.Count())*/];

            var endPoint = new IPEndPoint(ipAddress, discoveryService.RunningTcpPort);

            return endPoint;
        }

        //private EndPoint EndPointConnectedToInternet()
        //{
        //    var udpClient = new UdpClient(Dns.GetHostName(), discoveryService.RunningTcpPort);
        //    var endPointConnectedToInternet = udpClient.Client.LocalEndPoint as IPEndPoint;
        //    endPointConnectedToInternet.Port = discoveryService.RunningTcpPort;

        //    return endPointConnectedToInternet;
        //}

    }
}
