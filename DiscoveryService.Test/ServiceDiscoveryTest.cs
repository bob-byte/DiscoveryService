using LUC.DiscoveryService;
using LUC.DiscoveryService.Messages;
using NUnit.Framework;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DiscoveryService.Test
{
    [TestFixture]
    public class ServiceDiscoveryTest
    {
        private ServiceDiscovery serviceDiscovery;

        private readonly UInt32 protocolVersion = 1;

        [SetUp]
        public void SetupService()
        {
            serviceDiscovery = ServiceDiscovery.Instance(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion));
        }

        [TearDown]
        public void TearDownService()
        {
            serviceDiscovery?.Stop();
        }

        [Test]
        public void Ctor_PassNullPar_GetException()
        {
            Assert.That(code: () => serviceDiscovery = ServiceDiscovery.Instance(null), 
                constraint: Throws.TypeOf(expectedType: typeof(ArgumentNullException)));
        }

        [Test]
        public void QueryAllServices_GetOwnUdpMessage_DontGet()
        {
            var done = new ManualResetEvent(false);
            serviceDiscovery.Service.QueryReceived += (sender, e) =>
            {
                if (e.Message is MulticastMessage)
                {
                    done.Set();
                }
            };

            serviceDiscovery.Service.NetworkInterfaceDiscovered += (sender, e) => serviceDiscovery.QueryAllServices();

            try
            {
                serviceDiscovery.Start();
                Assert.IsFalse(done.WaitOne(TimeSpan.FromSeconds(value: 1)), message: "Got own UDP message");
            }
            finally
            {
                serviceDiscovery.Stop();
            }
        }

        [Test]
        public void StartAndStop_RoundTrip_WithoutExceptions()
        {
            serviceDiscovery.Stop();
            serviceDiscovery.Start();
            serviceDiscovery.Start();
            serviceDiscovery.Stop();
            serviceDiscovery.Stop();
            serviceDiscovery.Start();

            serviceDiscovery.QueryAllServices();
        }

        /// <summary>
        /// Sd - Service Discovery
        /// </summary>
        [Test]
        public void QueryAllServices_WhenSdIsntStarted_GetException()
        {
            Assert.That(code: () => serviceDiscovery.QueryAllServices(), Throws.TypeOf(typeof(InvalidOperationException)));
        }

        /// <summary>
        /// Sd - Service Discovery
        /// </summary>
        [Test]
        public void QueryAllServices_WhenSdIsStartedAndStopped_GetException()
        {
            serviceDiscovery.Start();
            serviceDiscovery.Stop();

            Assert.That(code: () => serviceDiscovery.QueryAllServices(), Throws.TypeOf(typeof(InvalidOperationException)));
        }

        [Test]
        public void MachineId_TwoCallsInstanceMethod_TheSameId()
        {
            var expected = serviceDiscovery.MachineId;

            var actual = ServiceDiscovery.Instance(new ServiceProfile(useIpv4: true, useIpv6: true, protocolVersion: 1)).MachineId;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void QueryAllServices_GetTcpMessage_NotFailed()
        {
            var done = new ManualResetEvent(false);
            serviceDiscovery.Start();
            serviceDiscovery.Service.AnswerReceived += (sender, e) =>
            {
                done.Set();
            };
            var availableIps = Service.GetIPAddresses().ToArray();

            serviceDiscovery.SendTcpMess(this, new MessageEventArgs
            {
                Message = new MulticastMessage(messageId: 123, serviceDiscovery.RunningTcpPort, protocolVersion, null),
                RemoteEndPoint = new IPEndPoint(availableIps[1], (Int32)serviceDiscovery.RunningTcpPort)
            });

            Assert.IsTrue(done.WaitOne());
        }

        [Test]
        public void SendTcpMess_SetParInTypeTcpMess_GetException()
        {
            serviceDiscovery.Start();

            Assert.That(() => serviceDiscovery.SendTcpMess(this, new MessageEventArgs
            {
                Message = new TcpMessage(messageId: 123, serviceDiscovery.RunningTcpPort, protocolVersion, null),
            }),
            Throws.TypeOf(typeof(ArgumentException)));
        }

        [Test]
        public void SendTcpMess_SendNullMessage_GetException()
        {
            serviceDiscovery.Start();

            Assert.That(() => serviceDiscovery.SendTcpMess(this, new MessageEventArgs
            {
                Message = null
            }),
            Throws.TypeOf(typeof(ArgumentException)));
        }

        [Test]
        public void SendTcpMess_SendNullRemoteEndPoint_GetException()
        {
            serviceDiscovery.Start();

            Assert.That(() => serviceDiscovery.SendTcpMess(this, new MessageEventArgs
            {
                Message = new MulticastMessage(messageId: 123, serviceDiscovery.RunningTcpPort, protocolVersion, null),
                RemoteEndPoint = null
            }),
            Throws.TypeOf(typeof(ArgumentException)));
        }

        [Test]
        public void SendTcpMess_EndPointIsDns_GetException()
        {
            serviceDiscovery.Start();

            Assert.That(() => serviceDiscovery.SendTcpMess(this, new MessageEventArgs
            {
                Message = new MulticastMessage(messageId: 123, serviceDiscovery.RunningTcpPort, protocolVersion, null),
                RemoteEndPoint = new DnsEndPoint(Dns.GetHostName(), (Int32)AbstractService.DefaultPort, AddressFamily.InterNetwork)
            }),
            Throws.TypeOf(typeof(ArgumentException)));
        }
    }
}
