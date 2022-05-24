using Serilog.Events;
using Serilog;

using System;
using Serilog.Core;
using LUC.Services.Implementation;
using LUC.Interfaces;
using LUC.Interfaces.Abstract;
using LUC.Interfaces.Models;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using System.ComponentModel.Composition;

namespace DiscoveryServices
{
    sealed class DsLogger : ConsoleLogger
    {
        private readonly Logger m_configuredDsLogger;

        public DsLogger()
        {
            String logFileNamePostfix = $"DS-{DateTime.Today.ToString( format: "d" ).Replace( oldChar: '/', newChar: '.' )}";

            SettingsService = AppSettings.ExportedValue<ISettingsService>();

            LoggerConfiguration loggerConfig = LoggerConfigExtension.BaseLucLoggerConfig( logFileNamePostfix, SettingsService.IsLogToTxtFile );
            loggerConfig = loggerConfig.WriteTo.Sink( logEventSink: new LoggerToServer( OsVersionHelper.Version() ), restrictedToMinimumLevel: LogEventLevel.Fatal );

            m_configuredDsLogger = loggerConfig.CreateLogger();
        }

        //we don't need to import value (see class ConsoleLogger)
        public override ISettingsService SettingsService { get; set; }

        public override void LogCriticalError( Exception exception )
        {
            base.LogCriticalError( exception );

            m_configuredDsLogger.Fatal( exception, exception.Message );
            if ( exception.InnerException != null )
            {
                m_configuredDsLogger.Fatal( exception.InnerException, exception.InnerException.Message );
            }
        }

        public override void LogCriticalError( String message, Exception exception )
        {
            base.LogCriticalError( message, exception );

            m_configuredDsLogger.Fatal( message );
            m_configuredDsLogger.Fatal( exception, exception.Message );
            if ( exception.InnerException != null )
            {
                m_configuredDsLogger.Fatal( exception.InnerException, exception.InnerException.Message );
            }
        }

        public override void LogFatal( String message )
        {
            base.LogFatal( message );
            m_configuredDsLogger.Fatal( message );
        }

        public override void LogError( Exception ex, String logRecord )
        {
            base.LogError( ex, logRecord );
            m_configuredDsLogger.Error( ex, logRecord );
        }

        public override void LogError( String logRecord )
        {
            base.LogError( logRecord );
            m_configuredDsLogger.Error( logRecord );
        }

        public override void LogInfo( String logRecord )
        {
            base.LogInfo( logRecord );
            m_configuredDsLogger.Information( logRecord );
        }
    }
}
