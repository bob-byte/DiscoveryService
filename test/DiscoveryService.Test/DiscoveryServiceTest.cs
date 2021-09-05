using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Test
{
    [TestFixture]
    public class DiscoveryServiceTest
    {
        private DiscoveryService discoveryService;

        [SetUp]
        public void SetupService()
        {
            discoveryService = new DiscoveryService(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1));
        }

        [TearDown]
        public void TearDownService()
        {
            discoveryService?.Stop();
        }

        [Test]
        public void Ctor_PassNullPar_GetException()
        {
            Assert.That(code: () => discoveryService = new DiscoveryService(null), 
                constraint: Throws.TypeOf(expectedType: typeof(ArgumentNullException)));
        }

        [Test]
        public void QueryAllServices_GetOwnUdpMessage_DontGet()
        {
            var done = new ManualResetEvent(initialState: false);
            discoveryService.Start();

            discoveryService.NetworkEventInvoker.QueryReceived += (sender, e) =>
            {
                if (e.Message<UdpMessage>() != null)
                {
                    done.Set();
                }
            };

            discoveryService.QueryAllServices();

            Assert.IsFalse(done.WaitOne(TimeSpan.FromSeconds(value: 1)), message: "Got own UDP message");
        }

        [Test]
        public void StartAndStop_RoundTrip_WithoutExceptions()
        {
            discoveryService.Stop();
            discoveryService.Start();
            discoveryService.Start();
            discoveryService.Stop();
            discoveryService.Stop();
            discoveryService.Start();

            discoveryService.QueryAllServices();
        }

        /// <summary>
        /// Sd - Service Discovery
        /// </summary>
        [Test]
        public void QueryAllServices_WhenSdIsntStarted_GetException()
        {
            Assert.That(code: () => discoveryService.QueryAllServices(), Throws.TypeOf(typeof(InvalidOperationException)));
        }

        /// <summary>
        /// Sd - Service Discovery
        /// </summary>
        [Test]
        public void QueryAllServices_WhenSdIsStartedAndStopped_GetException()
        {
            discoveryService.Start();
            discoveryService.Stop();

            Assert.That(code: () => discoveryService.QueryAllServices(), Throws.TypeOf(typeof(InvalidOperationException)));
        }

        [Test]
        public void MachineId_TwoCallsInstanceMethod_TheSameId()
        {
            var expected = discoveryService.MachineId;

            var actual = new DiscoveryService(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1)).MachineId;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void SendTcpMessageAsync_GetTcpMessage_NotFailed()
        {
            var done = new ManualResetEvent(false);
            discoveryService.Start();
            discoveryService.NetworkEventInvoker.AnswerReceived += (sender, e) =>
            {
                done.Set();
            };

            var availableIps = discoveryService.NetworkEventInvoker.RunningIpAddresses.ToArray();
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = new IPEndPoint(availableIps[1], discoveryService.RunningTcpPort)
            };
            eventArgs.SetMessage(new UdpMessage(messageId: 123, tcpPort: discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null));

            discoveryService.SendTcpMessageAsync(this, eventArgs).Wait();

            Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(value: 10)));
        }

        [Test]
        public void SendTcpMess_SetParInTypeTcpMess_GetException()
        {
            discoveryService.Start();
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = null
            };
            eventArgs.SetMessage(new AcknowledgeTcpMessage(messageId: 123, discoveryService.MachineId, ID.RandomID.Value, tcpPort: discoveryService.RunningTcpPort, protocolVersion: 1, groupsIds: null));

            Assert.That(() => discoveryService.SendTcpMessageAsync(this, eventArgs), Throws.TypeOf(typeof(ArgumentException)));
        }

        [Test]
        public void SendTcpMess_SendNullMessage_GetException()
        {
            discoveryService.Start();
            var eventArgs = new UdpMessageEventArgs();
            eventArgs.SetMessage<UdpMessage>(message: null);

            Assert.That(() => discoveryService.SendTcpMessageAsync(this, eventArgs), Throws.TypeOf(typeof(ArgumentException)));
        }

        [Test]
        public void SendTcpMess_SendNullRemoteEndPoint_GetException()
        {
            discoveryService.Start();
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = null
            };
            eventArgs.SetMessage(new UdpMessage(messageId: 123, tcpPort: discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null));

            Assert.That(() => discoveryService.SendTcpMessageAsync(this, eventArgs), Throws.TypeOf(typeof(ArgumentException)));
        }

        [Test]
        public void SendTcpMess_EndPointIsDns_GetException()
        {
            discoveryService.Start();
            var eventArgs = new UdpMessageEventArgs
            {
                RemoteEndPoint = new DnsEndPoint(Dns.GetHostName(), AbstractService.DefaultPort, AddressFamily.InterNetwork)
            };
            eventArgs.SetMessage(new UdpMessage(messageId: 123, tcpPort: discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null));

            Assert.That(() => discoveryService.SendTcpMessageAsync(this, eventArgs), Throws.TypeOf(typeof(ArgumentException)));
        }

        /// <summary>
        /// Ds - Discovery Service
        /// </summary>
        [Test]
        public void Instance_CreateOneMoreInstanceOfDsAndCompareMachineId_TheSameMachineId()
        {
            DiscoveryService discoveryService2 = null;
            Task.Run(() =>
            {
                discoveryService2 = new DiscoveryService(new ServiceProfile(useIpv4: true, useIpv6: true, 
                    protocolVersion: 2));
            }).Wait();

            Assert.IsTrue(discoveryService.MachineId == discoveryService2.MachineId);
        }
    }
}
