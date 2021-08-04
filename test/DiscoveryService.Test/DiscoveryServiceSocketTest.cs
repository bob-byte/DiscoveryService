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
        private DiscoveryService discoveryService;

        [SetUp]
        public void SetUpDiscoveryService()
        {
            discoveryService = DiscoveryService.Instance(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1));
        }

        [TearDown]
        public void StopDiscoveryService()
        {
            discoveryService.Stop();
        }

        [Test]
        public void Connect_RemoteEndPointIsNull_GetException()
        {
            var dsSocket = InitializedTcpSocket();

            Assert.That(() =>
                    dsSocket.Connect(remoteEndPoint: null, TimeSpan.FromSeconds(1)),
                    Throws.TypeOf(typeof(ArgumentNullException)));
        }

        [Test]
        public void A()
        {
            //connect with default timeout
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
            discoveryService.Start();

            var client = InitializedTcpSocket();

            var endPoint = AvailableIpAddress(discoveryService, client.AddressFamily);
            client.Connect(endPoint, Constants.ConnectTimeout);

            SendPingRequest(client, Constants.SendTimeout);

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
                Logging.log
            );

        private IPEndPoint AvailableIpAddress(DiscoveryService discoveryService, AddressFamily addressFamily)
        {
            var allPossibleIpAddresses = discoveryService.Service.RunningIpAddresses.Where(c => c.AddressFamily == addressFamily).ToArray();

            Random random = new Random();
            var ipAddress = allPossibleIpAddresses[1/*random.Next(allPossibleIpAddresses.Count())*/];

            var endPoint = new IPEndPoint(ipAddress, discoveryService.RunningTcpPort);

            return endPoint;
        }

        private void SendPingRequest(DiscoveryServiceSocket socket, TimeSpan timeout)
        {
            var pingRequest = InitializedPingRequest();
            socket.Send(pingRequest.ToByteArray(), timeout);
        }

        private PingRequest InitializedPingRequest()
        {
            var pingRequest = new PingRequest
            {
                MessageOperation = MessageOperation.Ping,
                RandomID = ID.RandomID.Value,
                Sender = ID.RandomIDInKeySpace.Value
            };

            return pingRequest;
        }

        //private EndPoint EndPointConnectedToInternet()
        //{
        //    var udpClient = new UdpClient(Dns.GetHostName(), discoveryService.RunningTcpPort);
        //    var endPointConnectedToInternet = udpClient.Client.LocalEndPoint as IPEndPoint;
        //    endPointConnectedToInternet.Port = discoveryService.RunningTcpPort;

        //    return endPointConnectedToInternet;
        //}

        [Test]
        public void Send_SendPingWithDefaultTimeout_DsReceivesPingRequest()
        {
            discoveryService.Start();

            var socket = InitializedTcpSocket();

            var endPoint = AvailableIpAddress(discoveryService, socket.AddressFamily);
            socket.Connect(endPoint, Constants.ConnectTimeout);

            ManualResetEvent receivedPingRequest = new ManualResetEvent(initialState: false);
            discoveryService.Service.PingReceived += (sender, eventArgs) =>
            {
                var message = eventArgs.Message<PingRequest>(whetherReadMessage: false);
                if (message != null)
                {
                    receivedPingRequest.Set();
                }
            };

            SendPingRequest(socket, timeout: default);

            Assert.IsTrue(receivedPingRequest.WaitOne(TimeSpan.FromSeconds(value: 2)), message: "DS didn\'t receive ping request");
        }

        [Test]
        public void Send_WithoutConnection_GetException()
        {
            var socket = InitializedTcpSocket();

            Assert.That(() => socket.Send(new Byte[] { 0, 1, 2 }, Constants.ReceiveTimeout), 
                Throws.TypeOf(typeof(InvalidOperationException)));
        }

        [Test]

    }
}
