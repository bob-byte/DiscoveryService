using LUC.Interfaces;
using LUC.Interfaces.Models;
using LUC.Services.Implementation;

namespace LUC.DiscoveryServices.Common
{
    internal static class DsLoggerSet
    {
        public static ILoggingService DefaultLogger { get; } = new DsLogger();

        public static ILoggingService ConsoleLogger { get; } = new ConsoleLogger
        {
            SettingsService = AppSettings.ExportedValue<ISettingsService>()
        };

        public static ILoggingService LucLogger { get; } = new LoggingService
        {
            SettingsService = AppSettings.ExportedValue<ISettingsService>()
        };
    }
}
