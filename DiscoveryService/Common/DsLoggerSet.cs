using LUC.Interfaces;
using LUC.Services.Implementation;

namespace DiscoveryServices.Common
{
    internal static class DsLoggerSet
    {
        public static ILoggingService DefaultLogger { get; } = new DsLogger();

        public static ILoggingService ConsoleLogger { get; } = new ConsoleLogger();

        public static ILoggingService LucLogger { get; } = new LoggingService();
    }
}
