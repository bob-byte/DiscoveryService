using LUC.Interfaces;
using LUC.Interfaces.Extensions;

using System;
using System.ComponentModel.Composition;

namespace LUC.Services.Implementation
{
    [Export( typeof( ILoggingService ) )]
    public class ConsoleLogger : ILoggingService
    {
        [Import( typeof( ISettingsService ) )]
        public virtual ISettingsService SettingsService { get; set; }

        public virtual void LogCriticalError( Exception exception )
        {
            if ( SettingsService?.IsShowConsole == true )
            {
                Console.WriteLine( exception.ToString() );
            }
        }

        public virtual void LogCriticalError( String message, Exception exception )
        {
            if ( SettingsService?.IsShowConsole == true )
            {
                Console.WriteLine( exception.ToString() );
                Console.WriteLine( message.WithAttention() );
            }
        }

        public virtual void LogFatal( String message )
        {
            if ( SettingsService?.IsShowConsole == true )
            {
                Console.WriteLine( message.WithAttention() );
            }
        }

        public virtual void LogError( Exception ex, String logRecord )
        {
            if ( SettingsService?.IsShowConsole == true )
            {
                Console.WriteLine( logRecord );
            }
        }

        public virtual void LogError( String logRecord )
        {
            if ( SettingsService?.IsShowConsole == true )
            {
                Console.WriteLine( logRecord );
            }
        }

        public virtual void LogInfo( String logRecord )
        {
            if ( SettingsService?.IsShowConsole == true )
            {
                Console.WriteLine( logRecord );
            }
        }

        public virtual void LogInfoWithLongTime( String logRecord )
        {
            logRecord = logRecord + " UTC:" + DateTime.UtcNow.ToLongTimeString();
            LogInfo( logRecord );
        }
    }
}
