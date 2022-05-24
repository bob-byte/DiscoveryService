using LUC.Interfaces.Extensions;
using LUC.Interfaces.InputContracts;
using LUC.Interfaces.Models;

using Newtonsoft.Json;

using Nito.AsyncEx.Synchronous;

using Serilog.Core;
using Serilog.Events;

using System;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace LUC.Services.Implementation
{
    public class LoggerToServer : ILogEventSink
    {
        private readonly String m_requestUri;

        private String m_host;
        private HttpClient m_httpClient;

        /// <summary>
        /// Initialize logger to server
        /// </summary>
        /// <param name="host">
        /// OS version
        /// </param>
        public LoggerToServer( String host )
        {
            m_host = host;
            m_requestUri = BuildUriExtensions.PostLogUri( AppSettings.RestApiHost );
            m_httpClient = new HttpClient();
        }

        ~LoggerToServer()
        {
            m_httpClient.Dispose();
        }

        /// <summary>
        /// OS version
        /// </summary>
        public String Host
        {
            get => m_host;
            set => Interlocked.Exchange( ref m_host, value );
        }

        public void Emit( LogEvent logEvent )
        {
            if ( logEvent != null )
            {
                HttpResponseMessage response = null;
                StringContent httpContent = null;

                LogRequest logRequest = logEvent.Exception == null ?
                    new LogRequest( logEvent.MessageTemplate.Text, Host ) :
                    new LogRequest( logEvent.Exception, Host );

                try
                {
                    String httpContentAsStr = JsonConvert.SerializeObject( logRequest, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore } );
                    httpContent = new StringContent( httpContentAsStr, Encoding.UTF8, mediaType: "application/json" );

                    response = m_httpClient.PostAsync( m_requestUri, httpContent ).WaitAndUnwrapException();
                }
                catch
                {
                    if ( ( httpContent != null ) && ( response?.IsSuccessStatusCode != true ) )
                    {
                        m_httpClient = new HttpClient();
                    }
                }
            }
        }
    }
}
