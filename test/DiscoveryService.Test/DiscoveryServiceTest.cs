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
            discoveryService = DiscoveryService.Instance(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1));
        }

        [TearDown]
        public void TearDownService()
        {
            discoveryService?.Stop();
        }

        [Test]
        public void Ctor_PassNullPar_GetException()
        {
            Assert.That(code: () => discoveryService = DiscoveryService.Instance(null), 
                constraint: Throws.TypeOf(expectedType: typeof(ArgumentNullException)));
        }

        [Test]
        public void QueryAllServices_GetOwnUdpMessage_DontGet()
        {
            var done = new ManualResetEvent(false);
            discoveryService.Service.QueryReceived += (sender, e) =>
            {
                if (e.Buffer is UdpMessage)
                {
                    done.Set();
                }
            };

            discoveryService.Service.NetworkInterfaceDiscovered += (sender, e) => discoveryService.QueryAllServices();

            try
            {
                discoveryService.Start();
                Assert.IsFalse(done.WaitOne(TimeSpan.FromSeconds(value: 1)), message: "Got own UDP message");
            }
            finally
            {
                discoveryService.Stop();
            }
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

            var actual = DiscoveryService.Instance(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1)).MachineId;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void QueryAllServices_GetTcpMessage_NotFailed()
        {
            var done = new ManualResetEvent(false);
            discoveryService.Start();
            discoveryService.Service.AnswerReceived += (sender, e) =>
            {
                done.Set();
            };
            var availableIps = NetworkEventHandler.GetIPAddresses().ToArray();

            discoveryService.SendTcpMessage(this, new TcpMessageEventArgs
            {
                Buffer = new UdpMessage(messageId: 123,tcpPort: discoveryService.RunningTcpPort,protocolVersion: 1,machineId:  null),
                RemoteContact = new IPEndPoint(availableIps[1], (Int32)discoveryService.RunningTcpPort)
            });

            Assert.IsTrue(done.WaitOne());
        }

        [Test]
        public void SendTcpMess_SetParInTypeTcpMess_GetException()
        {
            discoveryService.Start();

            Assert.That(() => discoveryService.SendTcpMessage(this, new TcpMessageEventArgs
            {
                Buffer = new TcpMessage(messageId: 123, discoveryService.MachineId, tcpPort: discoveryService.RunningTcpPort, protocolVersion: 1,groupsIds: null),
            }),
            Throws.TypeOf(typeof(ArgumentException)));
        }

        [Test]
        public void SendTcpMess_SendNullMessage_GetException()
        {
            discoveryService.Start();

            Assert.That(() => discoveryService.SendTcpMessage(this, new TcpMessageEventArgs
            {
                Buffer = null
            }),
            Throws.TypeOf(typeof(ArgumentException)));
        }

        [Test]
        public void SendTcpMess_SendNullRemoteEndPoint_GetException()
        {
            discoveryService.Start();

            Assert.That(() => discoveryService.SendTcpMessage(this, new TcpMessageEventArgs
            {
                Buffer = new UdpMessage(messageId: 123, tcpPort: discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null),
                RemoteContact = null
            }),
            Throws.TypeOf(typeof(ArgumentException)));
        }

        [Test]
        public void SendTcpMess_EndPointIsDns_GetException()
        {
            discoveryService.Start();

            Assert.That(() => discoveryService.SendTcpMessage(this, new TcpMessageEventArgs
            {
                Buffer = new UdpMessage(messageId: 123, tcpPort: discoveryService.RunningTcpPort, protocolVersion: 1, machineId: null),
                RemoteContact = new DnsEndPoint(Dns.GetHostName(), (Int32)AbstractService.DefaultPort, AddressFamily.InterNetwork)
            }),
            Throws.TypeOf(typeof(ArgumentException)));
        }

        /// <summary>
        /// Ds - Discovery Service
        /// </summary>
        [Test]
        public void Instance_CreateOneMoreInstanceOfDsAndCompareMachineId_TheSameMachineId()
        {
            //create another task, call DS.Instance
            DiscoveryService discoveryService2 = null;
            Task.Run(() =>
            {
                discoveryService2 = DiscoveryService.Instance(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 2));
            }).Wait();

            Assert.IsTrue(discoveryService.MachineId == discoveryService2.MachineId);
            //compare MachineIds. They should be different 
        }
    }
}
