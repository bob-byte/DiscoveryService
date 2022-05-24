using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;

using NUnit.Framework;

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LUC.DiscoveryServices.Test
{
    [TestFixture]
    class DiscoveryServiceSocketTest
    {
        [Test]
        public void Connect_RemoteEndPointIsNull_GetException()
        {
            AsyncSocket dsSocket = InitializedTcpSocket();

            Assert.That( () =>
                     dsSocket.DsConnect( remoteEndPoint: null, TimeSpan.FromSeconds( 1 ) ),
                    Throws.TypeOf( typeof( ArgumentNullException ) ) );
        }

        [Test]
        public void ConnectAsync_RemoteEndPointIsNull_GetException()
        {
            AsyncSocket dsSocket = InitializedTcpSocket();

            Assert.That( async () =>
                     await dsSocket.DsConnectAsync( remoteEndPoint: null, TimeSpan.FromSeconds( 1 ) ),
                    Throws.TypeOf( typeof( ArgumentNullException ) ) );
        }

        [Test]
        public void Receive_SendPingRequestAndReceiveResponse_NotFailed()
        {
            DiscoveryService discoveryService = DsSetUpTests.DiscoveryService;
            discoveryService.Start();

            AsyncSocket client = InitializedTcpSocket();

            IPEndPoint endPoint = AvailableIpAddress( discoveryService, client.AddressFamily );
            client.DsConnect( endPoint, DsConstants.ConnectTimeout );

            var pingRequest = new PingRequest( KademliaId.RandomIDInKeySpace.Value, discoveryService.MachineId );

            client.DsSend( pingRequest.ToByteArray(), DsConstants.SendTimeout );

            Thread.Sleep( TimeSpan.FromSeconds( value: 5 ) );
            if ( client.Available > 0 )
            {
                Byte[] receivedBytes = client.DsReceive( DsConstants.ReceiveTimeout );

                Assert.IsTrue( receivedBytes.Length > 0 );
            }
            else
            {
                throw new TimeoutException( $"Timeout to receive {typeof( PingResponse ).Name} data" );
            }
        }

        private AsyncSocket InitializedTcpSocket() =>
            new AsyncSocket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );

        private IPEndPoint AvailableIpAddress( DiscoveryService discoveryService, AddressFamily addressFamily )
        {
            IPAddress[] allPossibleIpAddresses = discoveryService.NetworkEventInvoker.ReachableIpAddresses.Where( c => c.AddressFamily == addressFamily ).ToArray();

            var random = new Random();
            IPAddress ipAddress = allPossibleIpAddresses[ 1/*random.Next(allPossibleIpAddresses.Count())*/];

            var endPoint = new IPEndPoint( ipAddress, discoveryService.RunningTcpPort );

            return endPoint;
        }

        [Test]
        public void SendAsync_WithoutSettingConnection_ThrowSocketException()
        {
            AsyncSocket socket = InitializedTcpSocket();

            Byte[] bytesToSend = new Byte[ 20 ];
            var random = new Random();
            random.NextBytes( bytesToSend );

            var waitIndefinitely = TimeSpan.FromMilliseconds( value: -1 );
            Assert.That(
                async () => await socket.DsSendAsync( bytesToSend, waitIndefinitely ).ConfigureAwait( continueOnCapturedContext: false ),
                Throws.TypeOf( typeof( SocketException ) )
            );
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
