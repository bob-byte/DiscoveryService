using Serilog.Events;
using Serilog;

using System;
using LUC.Interfaces.Models;

namespace LUC.Interfaces.Extensions
{
    public static class LoggerConfigExtension
    {
        public static LoggerConfiguration BaseLucLoggerConfig( String logFilePostfix, Boolean isLogToTxtFile )
        {
            var loggerConfig = new LoggerConfiguration();
            loggerConfig = loggerConfig.
                MinimumLevel.
                Debug().
                Enrich.
                FromLogContext();

            if ( isLogToTxtFile )
            {
                loggerConfig = loggerConfig.
                    WriteTo.File( path: $"{AppSettings.PathToLogFiles}\\Debug-{logFilePostfix}.txt", restrictedToMinimumLevel: LogEventLevel.Debug, flushToDiskInterval: AppSettings.FlushToDiskInterval ).
                    WriteTo.File( $"{AppSettings.PathToLogFiles}\\Info-{logFilePostfix}.txt", restrictedToMinimumLevel: LogEventLevel.Information, flushToDiskInterval: AppSettings.FlushToDiskInterval ).
                    WriteTo.File( $"{AppSettings.PathToLogFiles}\\Warning-{logFilePostfix}.txt", restrictedToMinimumLevel: LogEventLevel.Warning, flushToDiskInterval: AppSettings.FlushToDiskInterval ).
                    WriteTo.File( $"{AppSettings.PathToLogFiles}\\Error-{logFilePostfix}.txt", restrictedToMinimumLevel: LogEventLevel.Error, flushToDiskInterval: AppSettings.FlushToDiskInterval ).
                    WriteTo.File( $"{AppSettings.PathToLogFiles}\\Fatal-{logFilePostfix}.txt", restrictedToMinimumLevel: LogEventLevel.Fatal, flushToDiskInterval: AppSettings.FlushToDiskInterval );
            }

            return loggerConfig;
        }
    }
}