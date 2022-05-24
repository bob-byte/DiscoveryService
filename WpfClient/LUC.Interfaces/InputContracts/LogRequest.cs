using Newtonsoft.Json;

using System;

namespace LUC.Interfaces.InputContracts
{
    public class LogRequest
    {
        /// <summary>
        /// Initialize log request to server
        /// </summary>
        /// <param name="exception">
        /// Critical exception
        /// </param>
        /// <param name="host">
        /// OS version
        /// </param>
        public LogRequest( Exception exception, String host )
        {
            ExceptionName = exception.GetType().Name;
            StackTrace = exception.ToString();

            Host = host;
        }

        public LogRequest( String message, String host )
        {
            ExceptionName = message;
            Host = host;
        }

        [JsonProperty( propertyName: "exception_name" )]
        public String ExceptionName { get; }

        [JsonProperty( "traceback" )]
        public String StackTrace { get; }

        [JsonProperty( "host" )]
        public String Host { get; }
    }
}
