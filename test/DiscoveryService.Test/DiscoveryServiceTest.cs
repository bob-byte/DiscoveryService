using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;

using NUnit.Framework;

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Test
{
    [TestFixture]
    public class DiscoveryServiceTest
    {
        private DiscoveryService m_discoveryService;

        [SetUp]
        public void SetupService() => m_discoveryService = new DiscoveryService( new ServiceProfile( useIpv4: true, useIpv6: true, protocolVersion: 1 ) );

        [TearDown]
        public void TearDownService() => m_discoveryService?.Stop();

        [Test]
        public void Ctor_PassNullPar_GetException() => Assert.That( code: () => m_discoveryService = new DiscoveryService( null ),
                constraint: Throws.TypeOf( expectedType: typeof( ArgumentNullException ) ) );

        [Test]
        public void QueryAllServices_GetOwnUdpMessage_DontGet()
        {
            ManualResetEvent done = new ManualResetEvent( initialState: false );
            m_discoveryService.Start();

            m_discoveryService.NetworkEventInvoker.QueryReceived += ( sender, e ) =>
            {
                if ( e.Message<UdpMessage>() != null )
                {
                    done.Set();
                }
            };

            m_discoveryService.QueryAllServices();

            Assert.IsFalse( done.WaitOne( TimeSpan.FromSeconds( value: 1 ) ), message: "Got own UDP message" );
        }

        [Test]
        public void StartAndStop_RoundTrip_WithoutExceptions()
        {
            m_discoveryService.Stop();
            m_discoveryService.Start();
            m_discoveryService.Start();
            m_discoveryService.Stop();
            m_discoveryService.Stop();
            m_discoveryService.Start();

            m_discoveryService.QueryAllServices();
        }

        /// <summary>
        /// Sd - Service Discovery
        /// </summary>
        [Test]
        public void QueryAllServices_WhenSdIsntStarted_GetException() => Assert.That( code: () => m_discoveryService.QueryAllServices(), Throws.TypeOf( typeof( InvalidOperationException ) ) );

        /// <summary>
        /// Sd - Service Discovery
        /// </summary>
        [Test]
        public void QueryAllServices_WhenSdIsStartedAndStopped_GetException()
        {
            m_discoveryService.Start();
            m_discoveryService.Stop();

            Assert.That( code: () => m_discoveryService.QueryAllServices(), Throws.TypeOf( typeof( InvalidOperationException ) ) );
        }

        [Test]
        public void MachineId_TwoCallsInstanceMethod_TheSameId()
        {
            String expected = m_discoveryService.MachineId;

            String actual = new DiscoveryService( new ServiceProfile( useIpv4: true, useIpv6: true, protocolVersion: 1 ) ).MachineId;

            Assert.AreEqual( expected, actual );
        }

        [Test]
        public async Task SendTcpMessageAsync_GetTcpMessage_NotFailed()
        {
            ManualResetEvent done = new ManualResetEvent( false );
            m_discoveryService.Start();
            m_discoveryService.NetworkEventInvoker.AnswerReceived += ( sender, e ) => done.Set();

            IPAddress[] availableIps = m_discoveryService.NetworkEventInvoker.RunningIpAddresses.ToArray();
            UdpMessageEventArgs eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = new IPEndPoint( availableIps[ 1 ], m_discoveryService.RunningTcpPort )
            };

            eventArgs.SetMessage( new UdpMessage( messageId: 123, tcpPort: m_discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null ) );

            await m_discoveryService.SendTcpMessageAsync( sender: this, eventArgs ).ConfigureAwait(continueOnCapturedContext: false);

            Assert.IsTrue( done.WaitOne( TimeSpan.FromSeconds( value: 10 ) ) );
        }

        [Test]
        public void SendTcpMess_SetParInTypeTcpMess_GetException()
        {
            m_discoveryService.Start();
            UdpMessageEventArgs eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = null
            };
            eventArgs.SetMessage( new AcknowledgeTcpMessage( messageId: 123, m_discoveryService.MachineId, KademliaId.RandomID.Value, tcpPort: m_discoveryService.RunningTcpPort, protocolVersion: 1, groupsIds: null ) );

            Assert.That( () => m_discoveryService.SendTcpMessageAsync( this, eventArgs ), Throws.TypeOf( typeof( ArgumentException ) ) );
        }

        [Test]
        public void SendTcpMess_SendNullMessage_GetException()
        {
            m_discoveryService.Start();
            UdpMessageEventArgs eventArgs = new UdpMessageEventArgs();
            eventArgs.SetMessage<UdpMessage>( message: null );

            Assert.That( () => m_discoveryService.SendTcpMessageAsync( this, eventArgs ), Throws.TypeOf( typeof( ArgumentException ) ) );
        }

        [Test]
        public void SendTcpMess_SendNullRemoteEndPoint_GetException()
        {
            m_discoveryService.Start();
            UdpMessageEventArgs eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = null
            };
            eventArgs.SetMessage( new UdpMessage( messageId: 123, tcpPort: m_discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null ) );

            Assert.That( () => m_discoveryService.SendTcpMessageAsync( this, eventArgs ), Throws.TypeOf( typeof( ArgumentException ) ) );
        }

        [Test]
        public void SendTcpMess_EndPointIsDns_GetException()
        {
            m_discoveryService.Start();
            UdpMessageEventArgs eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = new DnsEndPoint( Dns.GetHostName(), AbstractService.DEFAULT_PORT, AddressFamily.InterNetwork )
            };
            eventArgs.SetMessage( new UdpMessage( messageId: 123, tcpPort: m_discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null ) );

            Assert.That( () => m_discoveryService.SendTcpMessageAsync( this, eventArgs ), Throws.TypeOf( typeof( ArgumentException ) ) );
        }

        /// <summary>
        /// Ds - Discovery Service
        /// </summary>
        [Test]
        public void Instance_CreateOneMoreInstanceOfDsAndCompareMachineId_TheSameMachineId()
        {
            DiscoveryService discoveryService2 = null;
            Task.Run( () =>
             {
                 discoveryService2 = new DiscoveryService( new ServiceProfile( useIpv4: true, useIpv6: true,
                     protocolVersion: 2 ) );
             } ).Wait();

            Assert.IsTrue( m_discoveryService.MachineId == discoveryService2.MachineId );
        }
    }
}
