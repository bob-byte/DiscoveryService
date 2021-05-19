using Common.Logging;
using Common.Logging.Configuration;
using Common.Logging.Simple;
using NUnit.Framework;

namespace DiscoveryService.Test
{
    [SetUpFixture]
    public static class Logging
    {
        [OneTimeSetUp]
        public static void AssemblyInitialize()
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

        [OneTimeTearDown]
        public static void AssemblyCleanup()
        {
            ;
        }
    }
}
