using LUC.DiscoveryService;
using LUC.DiscoveryService.Messages;
using NUnit.Framework;
using System;
using System.Threading;

namespace DiscoveryService.Test
{
    [TestFixture]
    public class ServiceDiscoveryTest
    {
        public TestContext TestContext { get; set; }

        [Test]
        public void QueryAllServices_GetOwnUdpMessage_DontGet()
        {
            var done = new ManualResetEvent(false);
            var discoveryService = ServiceDiscovery.Instance();
            discoveryService.Service.QueryReceived += (sender, e) =>
            {
                if (e.Message is MulticastMessage)
                {
                    done.Set();
                }
            };

            discoveryService.Service.NetworkInterfaceDiscovered += (sender, e) => discoveryService.QueryAllServices();

            try
            {
                discoveryService.Start(out _);
                Assert.IsFalse(done.WaitOne(TimeSpan.FromSeconds(value: 1)), message: "Got own UDP message");
            }
            finally
            {
                discoveryService.Stop();
            }
        }
    }
}
