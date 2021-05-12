using Common.Logging;
using Common.Logging.Configuration;
using Common.Logging.Simple;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DiscoveryService.Test
{
    [TestClass]
    public class Logging
    {
        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context)
        {
            //set logger factory
            var properties = new NameValueCollection
            {
                ["level"] = "TRACE",
                ["showLogName"] = "true",
                ["showDateTime"] = "true",
                ["dateTimeFormat"] = "HH:mm:ss.fff"
            };
            LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            ;
        }
    }
}
