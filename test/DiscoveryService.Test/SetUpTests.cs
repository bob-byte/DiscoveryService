using Common.Logging;
using Common.Logging.Configuration;
using Common.Logging.Simple;

using LUC.Interfaces;
using LUC.Services.Implementation;

using NUnit.Framework;

namespace LUC.DiscoveryService.Test
{
    [SetUpFixture]
    public static class SetUpTests
    {
        internal static ILoggingService LoggingService { get; private set; }

        [OneTimeSetUp]
        public static void AssemblyInitialize() =>
            //set logger factory
            LoggingService = new LoggingService
            {
                SettingsService = new SettingsService()
            };
    }
}
