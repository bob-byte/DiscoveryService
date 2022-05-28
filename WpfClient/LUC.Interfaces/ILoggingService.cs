using System;

namespace LUC.Interfaces
{
    //TODO: refactoring
    public interface ILoggingService
    {
        void LogCriticalError( Exception exception );

        void LogCriticalError( String message, Exception exception );

        void LogFatal( String message );

        void LogInfo( String logRecord );

        void LogError( String logRecord );

        void LogError( Exception ex, String logRecord );

        void LogInfoWithLongTime( String logRecord );
    }
}
