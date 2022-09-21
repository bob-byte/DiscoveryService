using AutoFixture;

using FluentAssertions;

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia.ClientPool;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Test.Builders;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Enums;
using LUC.Services.Implementation;

using NUnit.Framework;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Test
{
    [TestFixture]
    partial class DiscoveryServiceTest
    {
        private DiscoveryService m_discoveryService;

        public DiscoveryServiceTest()
        {
            m_discoveryService = DsSetUpTests.DiscoveryService;
        }

        [SetUp]
        public void SetupDs() =>
            m_discoveryService.Start();

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

            //create socket with reachable IP-address
            await fakeMessage.SendAsync( socket ).ConfigureAwait( false );

            await Task.Delay(TimeSpan.FromSeconds(value: 3)).ConfigureAwait(false);

            Boolean isMessageReceived = socket.Available > 0;
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
        public void Ctor_PassNullPar_GetArgumentNullException() => Assert.That( 
            code: () => m_discoveryService = DiscoveryService.Instance(
               null,
               GeneralConstants.PROTOCOL_VERSION,
               null,
               null
            ),
            constraint: Throws.TypeOf( expectedType: typeof( ArgumentNullException ) )
        );

#if !RECEIVE_UDP_FROM_OURSELF
        [Test]
        public void QueryAllServices_GetOwnUdpMessage_DontGet()
        {
            var done = new ManualResetEvent( initialState: false );
            m_discoveryService.Start();

            m_discoveryService.NetworkEventInvoker.QueryReceived += ( sender, e ) =>
            {
                if ( e.Message<AllNodesRecognitionMessage>() != null )
                {
                    done.Set();
                }
            };

            m_discoveryService.TryFindAllNodes();

            Assert.IsFalse( done.WaitOne( TimeSpan.FromSeconds( value: 1 ) ), message: "Got own UDP message" );
        }
#endif

        [Test]
        public void StartAndStop_RoundTrip_WithoutExceptions()
        {
            m_discoveryService.Stop(allowReuseService: true);
            m_discoveryService.Start();
            m_discoveryService.Start();
            m_discoveryService.Stop(true);
            m_discoveryService.Stop(true);
            m_discoveryService.Start();

            Assert.DoesNotThrow(m_discoveryService.TryFindAllNodes);
        }

        /// <summary>
        /// Sd - Service Discovery
        /// </summary>
        [Test]
        public void QueryAllServices_WhenSdIsntStarted_GetInvalidOperationException()
        {
            m_discoveryService.Stop(allowReuseService: true);
            Assert.That(code: m_discoveryService.TryFindAllNodes, Throws.TypeOf(typeof(InvalidOperationException)));
        }

        /// <summary>
        /// Sd - Service Discovery
        /// </summary>
        [Test]
        public void QueryAllServices_WhenSdIsStartedAndStopped_GetInvalidOperationException()
        {
            m_discoveryService.Start();
            m_discoveryService.Stop(allowReuseService: true);

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
        public async Task SendTcpMessageAsync_SendMessageAndWaitForReceiving_MessageReceived()
        {
            var done = new ManualResetEvent( false );

            m_discoveryService.NetworkEventInvoker.AnswerReceived += ( sender, e ) => done.Set();

            IPAddress[] availableIps = m_discoveryService.NetworkEventInvoker.ReachableIpAddresses.ToArray();
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = new IPEndPoint( availableIps[ 1 ], m_discoveryService.RunningTcpPort )
            };

            eventArgs.SetMessage( new AllNodesRecognitionMessage( messageId: 123, tcpPort: m_discoveryService.RunningTcpPort, protocolVersion: 1, machineId: DsSetUpTests.Fixture.Create<String>() ) );

            await m_discoveryService.SendAcknowledgeTcpMessageAsync( eventArgs, IoBehavior.Asynchronous ).ConfigureAwait( continueOnCapturedContext: false );

            //wait while FindNode executed (when DS receive AcknowledgeTcpMessage, it starts NetworkEventInvoker.Bootstrap)
            Assert.IsTrue( done.WaitOne(DsConstants.TimeWaitSocketReturnedToPool) );
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

            AssertSendAcknowledgeTcpMessageAsyncThrowArgException(eventArgs);
        }

        [Test]
        public void SendTcpMess_SendNullMessage_GetException()
        {
            m_discoveryService.Start();
            var eventArgs = new UdpMessageEventArgs();
            eventArgs.SetMessage<AllNodesRecognitionMessage>( message: null );

            AssertSendAcknowledgeTcpMessageAsyncThrowArgException(eventArgs);
        }

        [Test]
        public void SendTcpMess_SendNullRemoteEndPoint_GetException()
        {
            m_discoveryService.Start();
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = null
            };
            eventArgs.SetMessage( new AllNodesRecognitionMessage( messageId: 123, tcpPort: m_discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null ) );

            AssertSendAcknowledgeTcpMessageAsyncThrowArgException(eventArgs);
        }

        private void AssertSendAcknowledgeTcpMessageAsyncThrowArgException(UdpMessageEventArgs eventArgs) =>
            AssertSendAcknowledgeTcpMessageAsyncThrowException(eventArgs, typeof(ArgumentException));


        private void AssertSendAcknowledgeTcpMessageAsyncThrowException(UdpMessageEventArgs eventArgs, Type exceptionType = null)
        {
            Assert.That(() => m_discoveryService.SendAcknowledgeTcpMessageAsync(eventArgs, IoBehavior.Synchronous).ConfigureAwait(continueOnCapturedContext: false), Throws.TypeOf(exceptionType));
        }

        /// <summary>
        /// Ds - Discovery Service
        /// </summary>
        [Test]
        public void Instance_CreateOneMoreInstanceOfDsAndCompareMachineId_TheSameMachineId()
        {
            var discoveryService2 = DiscoveryService.Instance(
                MachineId.Create(),
                GeneralConstants.PROTOCOL_VERSION + 1,
                DsSetUpTests.CurrentUserProvider,
                userGroups: new ConcurrentDictionary<String, String>()
            );

            Assert.IsTrue( m_discoveryService.MachineId == discoveryService2.MachineId );
        }
    }
}
