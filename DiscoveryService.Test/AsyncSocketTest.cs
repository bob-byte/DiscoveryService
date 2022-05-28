using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;

using Nito.AsyncEx.Synchronous;

using NUnit.Framework;

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LUC.DiscoveryServices.Test
{
    [TestFixture]
    class AsyncSocketTest
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

            Assert.That( () =>
                    dsSocket.DsConnectAsync( remoteEndPoint: null, TimeSpan.FromSeconds( 1 ) ).WaitAndUnwrapException(),
                    Throws.TypeOf( typeof( ArgumentNullException ) ) );
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
                () => socket.DsSendAsync( bytesToSend, waitIndefinitely ).WaitAndUnwrapException(),
                Throws.TypeOf( typeof( SocketException ) )
            );
        }

        private AsyncSocket InitializedTcpSocket() =>
            new AsyncSocket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );

        private IPEndPoint AvailableIpAddress(DiscoveryService discoveryService, AddressFamily addressFamily)
        {
            IPAddress[] allPossibleIpAddresses = discoveryService.NetworkEventInvoker.ReachableIpAddresses.Where(c => c.AddressFamily == addressFamily).ToArray();

            IPAddress ipAddress = allPossibleIpAddresses[0];

            var endPoint = new IPEndPoint(ipAddress, discoveryService.RunningTcpPort);
            return endPoint;
        }
    }
}
