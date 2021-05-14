using LUC.DiscoveryService;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace DiscoveryService.Test
{
    [TestClass]
    public class ServiceDiscoveryTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Discover_AllServices()
        {
            var done = new ManualResetEvent(false);
            var discoveryService = ServiceDiscovery.Instance();

            try
            {
                discoveryService.Start(out _);
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(value: 1)), message: "DNS-SD query timeout");
            }
            finally
            {
                discoveryService.Stop();
            }
        }
    }
}
