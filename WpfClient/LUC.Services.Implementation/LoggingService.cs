using LUC.Interfaces;

using Serilog;

using System;
using System.ComponentModel.Composition;

namespace LUC.Services.Implementation
{
    [Export( typeof( ILoggingService ) )]
    public class LoggingService : ConsoleLogger
    {
        [Import(typeof(INotifyService))]
        public INotifyService NotifyService { get; set; }

        public override void LogCriticalError(Exception exception)
        {
            base.LogCriticalError(exception);

            Log.Error(exception, exception.Message);

            if ( exception.InnerException != null )
            {
                Log.Error(exception.InnerException, exception.InnerException.Message);
            }
        }

        public override void LogCriticalError(String message, Exception exception)
        {
            base.LogCriticalError(message, exception);

            Log.Error(exception, exception.Message);
            Log.Error(message);

            if ( exception.InnerException != null )
            {
                Log.Error(exception.InnerException, exception.InnerException.Message);
            }
        }

        public override void LogFatal( String message )
        {
            base.LogFatal( message );
            Log.Fatal( message );
        }

        public override void LogError( Exception ex, String logRecord )
        {
            Log.Error( ex, logRecord );
            base.LogError( ex, logRecord );
        }

        public override void LogError( String logRecord )
        {
            Log.Error( logRecord );
            base.LogError( logRecord );
        }

        public override void LogInfo( String logRecord )
        {
            Log.Information( logRecord );
            base.LogInfo( logRecord );
        }
    }
}
