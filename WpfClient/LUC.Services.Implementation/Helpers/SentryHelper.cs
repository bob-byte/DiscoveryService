using LUC.Interfaces.Models;

using Sentry;

using System;
using System.Reflection;

namespace LUC.Services.Implementation.Helpers
{
    public static class SentryHelper
    {
        public static void InitAppSentry()
        {
            using ( SentrySdk.Init( o => o.ConfigureLucOptions() ) )
            {
                ;//do nothing
            }
        }

        public static void ConfigureLucOptions( this SentryOptions sentryOptions )
        {
            var assembly = Assembly.GetEntryAssembly();
            sentryOptions.Configure( assembly );
        }

        public static void Configure( this SentryOptions sentryOptions, Assembly assembly )
        {
            sentryOptions.Dsn = AppSettings.SentryDsn;

            // Set traces_sample_rate to 1.0 to capture 100% of transactions for performance monitoring.
            // We recommend adjusting this value in production.
#if !DEBUG
            sentryOptions.TracesSampleRate = 1.0;
#endif

            sentryOptions.AttachStacktrace = true;
            sentryOptions.SendDefaultPii = true;

            Version assemblyVersion = assembly.GetName().Version;
            sentryOptions.Release = assemblyVersion.ToString();
        }
    }
}
