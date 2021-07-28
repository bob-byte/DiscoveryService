using Common.Logging;
using Common.Logging.Configuration;
using Common.Logging.Simple;
using LUC.Interfaces;
using LUC.Services.Implementation;
using NUnit.Framework;

namespace LUC.DiscoveryService.Test
{
    [SetUpFixture]
    public static class Logging
    {
        internal static ILoggingService log;

        [OneTimeSetUp]
        public static void AssemblyInitialize()
        {
            //set logger factory
            log = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        [OneTimeTearDown]
        public static void AssemblyCleanup()
        {
            ;
        }
    }
}
