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
            DiscoveryService discoveryService = new DiscoveryService( new ServiceProfile( useIpv4: true, useIpv6: true, protocolVersion: 1 ) );
            discoveryService.Start();

            AsyncSocket client = InitializedTcpSocket();

            IPEndPoint endPoint = AvailableIpAddress( discoveryService, client.AddressFamily );
            client.DsConnect( endPoint, Constants.ConnectTimeout );

            PingRequest pingRequest = new PingRequest( KademliaId.RandomIDInKeySpace.Value, discoveryService.MachineId );

            client.DsSend( pingRequest.ToByteArray(), Constants.SendTimeout );

            Thread.Sleep( TimeSpan.FromSeconds( value: 5 ) );
            if ( client.Available > 0 )
            {
                Byte[] receivedBytes = client.DsReceive( timeout: default );

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
                ProtocolType.Tcp,
                SetUpTests.LoggingService
            );

        private IPEndPoint AvailableIpAddress( DiscoveryService discoveryService, AddressFamily addressFamily )
        {
            IPAddress[] allPossibleIpAddresses = discoveryService.NetworkEventInvoker.RunningIpAddresses.Where( c => c.AddressFamily == addressFamily ).ToArray();

            Random random = new Random();
            IPAddress ipAddress = allPossibleIpAddresses[ 1/*random.Next(allPossibleIpAddresses.Count())*/];

            IPEndPoint endPoint = new IPEndPoint( ipAddress, discoveryService.RunningTcpPort );

            return endPoint;
        }

        [Test]
        public void SendAsync_WithoutSettingConnection_ThrowSocketException()
        {
            AsyncSocket socket = InitializedTcpSocket();

            Byte[] bytesToSend = new Byte[ 20 ];
            Random random = new Random();
            random.NextBytes( bytesToSend );

            TimeSpan waitIndefinitely = TimeSpan.FromMilliseconds( value: -1 );
            Assert.That(
                async () =>
                {
                    await socket.DsSendAsync( bytesToSend, waitIndefinitely ).ConfigureAwait( continueOnCapturedContext: false );
                },
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
