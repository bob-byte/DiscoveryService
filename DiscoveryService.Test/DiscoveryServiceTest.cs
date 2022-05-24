using AutoFixture;

using FluentAssertions;

using DiscoveryServices.Common;
using DiscoveryServices.Kademlia.ClientPool;
using DiscoveryServices.Messages;
using DiscoveryServices.Test.Builders;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Services.Implementation;

using NUnit.Framework;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DiscoveryServices.Test
{
    [TestFixture]
    partial class DiscoveryServiceTest
    {
        private DiscoveryService m_discoveryService;

        [OneTimeSetUp]
        public void SetupService()
        {
            m_discoveryService = DsSetUpTests.DiscoveryService;
            m_discoveryService.Start();
        }

        [OneTimeTearDown]
        public void TearDownService() => m_discoveryService?.Stop();

        [Test]
        public void BeforeCreatedInstance_CreateDifferentProtocolVersionAndTryToGetAccordingDs_ThrowsArgumentException()
        {
            UInt16 protocolVersion;
            do
            {
                protocolVersion = DsSetUpTests.Fixture.Create<UInt16>();
            }
            while ( protocolVersion == DsSetUpTests.DefaultProtocolVersion );

            Assert.That(
                code: () => m_discoveryService = DiscoveryService.BeforeCreatedInstance( protocolVersion ),
                constraint: Throws.TypeOf( expectedType: typeof( ArgumentException )
            ) );
        }

        [Test]
        public async Task ListenTcp_SendTooBigMessage_ItShoudntBeReceived()
        {
            m_discoveryService.Start();

            ConnectionPool connectionPool = DsSetUpTests.Fixture.Create<ConnectionPool>();

            var endPointBuilder = new EndPointsBuilder( BuildEndPointRequest.ReachableDsEndPoint );
            EndPoint socketId = endPointBuilder.Create<IPEndPoint>();

            Socket socket = await connectionPool.SocketAsync( socketId, DsConstants.ConnectTimeout, IoBehavior.Synchronous, DsConstants.TimeWaitSocketReturnedToPool ).
                ConfigureAwait( continueOnCapturedContext: false );

            //create random bytes with Constants.MAX_AVAILABLE_READ_BYTES count
            Int32 byteCount = DsConstants.MAX_AVAILABLE_READ_BYTES + 1;
            var fakeMessage = new FakeMessage( byteCount );

            //create AutoResetEvent for setting in TcpReceived
            var fakeMessageReceived = new AutoResetEvent( initialState: false );

            m_discoveryService.Start();
            m_discoveryService.NetworkEventInvoker.AnswerReceived += ( sender, eventArgs ) => fakeMessageReceived.Set();

            //create socket with reachable IP-address
            await fakeMessage.SendAsync( socket ).ConfigureAwait( false );

            //AutoResetEvent shouldn't be set
            Boolean isMessageReceived = fakeMessageReceived.WaitOne( TimeSpan.FromSeconds( value: 400 ) );
            isMessageReceived.Should().BeFalse();
        }

        [Test]
        public void SupportedBuckets_AddOneItemUsingCollectionFunc_ShoudntBeAddedToDs()
        {
            var specimens = new Fixture();
            String randomBucketLocalName = specimens.Create<String>();
            String randomSslCert = specimens.Create<String>();

            m_discoveryService.LocalBuckets.TryAdd( randomBucketLocalName, randomSslCert );

            m_discoveryService.LocalBuckets.ContainsKey( randomBucketLocalName ).Should().BeFalse();
        }

        [Test]
        public void Ctor_PassNullPar_GetException() => Assert.That( code: (TestDelegate)( () => m_discoveryService = DiscoveryService.Instance( null, null ) ),
            constraint: Throws.TypeOf( expectedType: typeof( ArgumentNullException ) ) );

        [Test]
        public void QueryAllServices_GetOwnUdpMessage_DontGet()
        {
            var done = new ManualResetEvent( initialState: false );
            m_discoveryService.Start();

            m_discoveryService.NetworkEventInvoker.QueryReceived += ( sender, e ) =>
            {
                if ( e.Message<MulticastMessage>() != null )
                {
                    done.Set();
                }
            };

            m_discoveryService.TryFindAllNodes();

            Assert.IsFalse( done.WaitOne( TimeSpan.FromSeconds( value: 1 ) ), message: "Got own UDP message" );
        }

        [Test]
#pragma warning disable S2699 // Tests should include assertions
        public void StartAndStop_RoundTrip_WithoutExceptions()
#pragma warning restore S2699 // Tests should include assertions
        {
            m_discoveryService.Stop();
            m_discoveryService.Start();
            m_discoveryService.Start();
            m_discoveryService.Stop();
            m_discoveryService.Stop();
            m_discoveryService.Start();

            m_discoveryService.TryFindAllNodes();
        }

        /// <summary>
        /// Sd - Service Discovery
        /// </summary>
        [Test]
        public void QueryAllServices_WhenSdIsntStarted_GetException() => Assert.That( code: (TestDelegate)(() => m_discoveryService.TryFindAllNodes()), Throws.TypeOf( typeof( InvalidOperationException ) ) );

        /// <summary>
        /// Sd - Service Discovery
        /// </summary>
        [Test]
        public void QueryAllServices_WhenSdIsStartedAndStopped_GetException()
        {
            m_discoveryService.Start();
            m_discoveryService.Stop();

            Assert.That( code: () => m_discoveryService.TryFindAllNodes(), Throws.TypeOf( typeof( InvalidOperationException ) ) );
        }

        [Test]
        public void MachineId_TwoCallsInstanceMethod_TheSameId()
        {
            String expected = m_discoveryService.MachineId;

            String actual = DsSetUpTests.DiscoveryService.MachineId;

            Assert.AreEqual( expected, actual );
        }

        [Test]
        public async Task SendTcpMessageAsync_GetTcpMessage_NotFailed()
        {
            var done = new ManualResetEvent( false );
            m_discoveryService.Start();
            m_discoveryService.NetworkEventInvoker.AnswerReceived += ( sender, e ) => done.Set();

            IPAddress[] availableIps = m_discoveryService.NetworkEventInvoker.ReachableIpAddresses.ToArray();
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = new IPEndPoint( availableIps[ 1 ], m_discoveryService.RunningTcpPort )
            };

            eventArgs.SetMessage( new MulticastMessage( messageId: 123, tcpPort: m_discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null ) );

            await m_discoveryService.SendAcknowledgeTcpMessageAsync( sender: this, eventArgs ).ConfigureAwait( continueOnCapturedContext: false );

            Assert.IsTrue( done.WaitOne( TimeSpan.FromSeconds( value: 10 ) ) );
        }

        [Test]
        public void SendTcpMess_SetParInTypeTcpMess_GetException()
        {
            m_discoveryService.Start();
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = null
            };
            eventArgs.SetMessage( new AcknowledgeTcpMessage( messageId: 123, m_discoveryService.MachineId, KademliaId.Random().Value, tcpPort: m_discoveryService.RunningTcpPort, protocolVersion: 1, groupsIds: null ) );

            Assert.That( () => m_discoveryService.SendAcknowledgeTcpMessageAsync( this, eventArgs ), Throws.TypeOf( typeof( ArgumentException ) ) );
        }

        [Test]
        public void SendTcpMess_SendNullMessage_GetException()
        {
            m_discoveryService.Start();
            var eventArgs = new UdpMessageEventArgs();
            eventArgs.SetMessage<MulticastMessage>( message: null );

            Assert.That( () => m_discoveryService.SendAcknowledgeTcpMessageAsync( this, eventArgs ), Throws.TypeOf( typeof( ArgumentException ) ) );
        }

        [Test]
        public void SendTcpMess_SendNullRemoteEndPoint_GetException()
        {
            m_discoveryService.Start();
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = null
            };
            eventArgs.SetMessage( new MulticastMessage( messageId: 123, tcpPort: m_discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null ) );

            Assert.That( () => m_discoveryService.SendAcknowledgeTcpMessageAsync( this, eventArgs ), Throws.TypeOf( typeof( ArgumentException ) ) );
        }

        [Test]
        public void SendTcpMess_EndPointIsDns_GetException()
        {
            m_discoveryService.Start();
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = new DnsEndPoint( Dns.GetHostName(), DsConstants.DEFAULT_PORT, AddressFamily.InterNetwork )
            };
            eventArgs.SetMessage( new MulticastMessage( messageId: 123, tcpPort: m_discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null ) );

            Assert.That( () => m_discoveryService.SendAcknowledgeTcpMessageAsync( this, eventArgs ), Throws.TypeOf( typeof( ArgumentException ) ) );
        }

        /// <summary>
        /// Ds - Discovery Service
        /// </summary>
        [Test]
        public void Instance_CreateOneMoreInstanceOfDsAndCompareMachineId_TheSameMachineId()
        {
            DiscoveryService discoveryService2 = DiscoveryService.Instance( new ServiceProfile( MachineId.Create(), useIpv4: true, useIpv6: true, protocolVersion: 2 ), DsSetUpTests.CurrentUserProvider );

            Assert.IsTrue( m_discoveryService.MachineId == discoveryService2.MachineId );
        }
    }
}
