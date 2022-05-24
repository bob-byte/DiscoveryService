
using LUC.Interfaces;

using Serilog;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace LUC.Services.Implementation
{
    public class RepeatableHttpClient : HttpClient
    {
        #region Constants

        private const Int32 MAX_RETRIES = 20;
        private const Int32 SECONDS_DELAY_429 = 3; // TODO 1.0 Add logic return to queue if it processed to long.
        private const Int32 SECONDS_DELAY_500 = 30;
        private const Int32 SECONDS_DELAY_502 = 30;
        private const Int32 SECONDS_DELAY_OTHER = 3;

        #endregion

        #region Properties

        [Import( typeof( ILoggingService ) )]
        public ILoggingService LoggingService { get; set; }

        public List<HttpStatusCode> RepeatRequestStatusCodes { get; set; }

        #endregion

        #region Constructors

        public RepeatableHttpClient() : base()
        {
            RepeatRequestStatusCodes = new List<HttpStatusCode>();
            Timeout = new TimeSpan( 0, 1, 0 );
        }

        public RepeatableHttpClient( String accessToken ) : base()
        {
            DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Token", accessToken );
            RepeatRequestStatusCodes = new List<HttpStatusCode>();
            Timeout = new TimeSpan( 0, 1, 0 );
        }

        public RepeatableHttpClient( String accessToken, Boolean disposeHandler ) : base( new HttpClientHandler(), disposeHandler )
        {
            DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Token", accessToken );
            RepeatRequestStatusCodes = new List<HttpStatusCode>();
            Timeout = new TimeSpan( 0, 1, 0 );
        }

        public RepeatableHttpClient( String accessToken, HttpStatusCode repeatRequestStatusCode ) : base()
        {
            DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Token", accessToken );
            RepeatRequestStatusCodes = new List<HttpStatusCode>
            {
                repeatRequestStatusCode
            };
            Timeout = new TimeSpan( 0, 1, 0 );
        }

        public RepeatableHttpClient( String accessToken, HttpStatusCode repeatRequestStatusCode, Boolean disposeHandler ) : base( new HttpClientHandler(), disposeHandler )
        {
            DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Token", accessToken );
            RepeatRequestStatusCodes = new List<HttpStatusCode>
            {
                repeatRequestStatusCode
            };
            Timeout = new TimeSpan( 0, 1, 0 );
        }

        #endregion

        #region Methods

        public async Task<HttpResponseMessage> SendRepeatableAsync( String requestUri, Func<Task<HttpContent>> contentReciever, HttpMethod method )
        {
            HttpResponseMessage response = null;

            for ( Int32 i = 0; i < MAX_RETRIES; i++ )
            {
                HttpRequestMessage request = null;

                try
                {
                    try
                    {
                        request = new HttpRequestMessage
                        {
                            RequestUri = new Uri( requestUri ),
                            Method = method
                        };

                        if ( contentReciever != null )
                        {
                            request.Content = await contentReciever().ConfigureAwait( continueOnCapturedContext: false );
                        }

#if DEBUG
                        var watch = Stopwatch.StartNew();
#endif

                        response = await SendAsync( request );

#if DEBUG
                        watch.Stop();
                        Int64 elapsedMs = watch.ElapsedMilliseconds;

                        if ( elapsedMs > 10000 )
                        {
                            Console.WriteLine( $"Responce time for request {requestUri} is {elapsedMs / 1000} sec." );
                        }

                        if ( response.IsSuccessStatusCode )
                        {
                            return response;
                        }
#endif
                    }
                    catch ( ObjectDisposedException )
                    {
                        Console.WriteLine( $"ObjectDisposedException occured. Try send request again..." );
                        await Task.Delay( millisecondsDelay: 1000 );
                        continue;
                    }
                    catch ( TaskCanceledException )
                    {
                        Console.WriteLine( $"TaskCanceledException occured. Try send request again..." );
                        await Task.Delay( 100 );
                        continue;
                    }
                    catch ( HttpRequestException )
                    {
                        Console.WriteLine( $"HttpRequestException occured. Try send request again..." );
                        await WriteLineRequestContent( request );
                        await Task.Delay( 60000 );
                        continue;
                    }
                    catch ( WebException )
                    {
                        Console.WriteLine( $@"WebException occurred. Try send request again..." );
                        await Task.Delay( 10000 );
                        continue;
                    }
                    catch ( Exception ex )
                    {
                        Console.WriteLine( ex.ToString(), ex.Message );
                        Log.Error( ex, ex.Message );
                    }

                    if ( response != null )
                    {
                        switch ( response.StatusCode )
                        {
                            case HttpStatusCode.InternalServerError:
                            {
                                Console.WriteLine( $@"Response 500. Try send request again... Request uri = '{requestUri}'" );
                                String stringRequest = await WriteLineRequestContent( request );//Exception: requestMessage.Content is disposed
                                String error = await response.Content.ReadAsStringAsync();
                                Log.Error( $"Response 500 from server. Error = '{error}', Request = '{stringRequest}'" );

                                await Task.Delay( SECONDS_DELAY_500 * 1000 );
                                break;
                            }
                            case (HttpStatusCode)429:
                            {
                                Console.WriteLine( $@"Response 429. Try send request again..." );
                                await WriteLineRequestContent( request );
                                String error = await response.Content.ReadAsStringAsync();
                                Console.WriteLine( $@"Response: {error}." );

                                await Task.Delay( SECONDS_DELAY_429 * 1000 );
                                break;
                            }
                            case HttpStatusCode.BadGateway:
                            {
                                Console.WriteLine( $@"Response 502. Try send request again..." );
                                await WriteLineRequestContent( request );
                                String error = await response.Content.ReadAsStringAsync();
                                Console.WriteLine( $@"Response: {error}" );

                                await Task.Delay( SECONDS_DELAY_502 * 1000 );
                                break;
                            }
                            default:
                            {
                                if ( RepeatRequestStatusCodes.Contains( response.StatusCode ) )
                                {
                                    Console.WriteLine( $@"Response: {response.StatusCode}. Try send request again..." );
                                    await WriteLineRequestContent( request );
                                    await Task.Delay( SECONDS_DELAY_OTHER );
#if DEBUG
                                    String error = await response.Content.ReadAsStringAsync();

                                    if ( error.Contains( "4" ) )
                                    {
                                        Console.WriteLine( $@"Response: {error}. Contains 4" );
                                    }
#endif
                                }
                                else
                                {
                                    // TODO Add loggging of Ellapsedtime from headers.
                                    return response;
                                }

                                break;
                            }
                        }
                    }
                }
                catch ( ObjectDisposedException ex )
                {
                    LoggingService.LogError( ex.Message );
                    await Task.Delay( 1000 );
                }
            }

            var result = new HttpResponseMessage( HttpStatusCode.InternalServerError )
            {
                Content = new StringContent( "Artificial error 500" )
            };
            return result;
        }

        private async Task<String> WriteLineRequestContent( HttpRequestMessage requestMessage )
        {
            if ( requestMessage != null && requestMessage.Content != null )
            {
                String requestContent = await requestMessage.Content.ReadAsStringAsync();
                Console.WriteLine( "Request content = " + requestContent );
                return requestContent;
            }

            return null;
        }

        public static async Task<HttpContent> CloneHttpContentAsync( HttpContent content )
        {
            if ( content == null )
            {
                return null;
            }

            var ms = new MemoryStream();

            try
            {
                await content.CopyToAsync( ms ).ConfigureAwait( continueOnCapturedContext: false );
            }
            catch ( ObjectDisposedException )
            {
                return null;
            }

            ms.Position = 0;

            var clone = new StreamContent( ms );
            foreach ( KeyValuePair<String, IEnumerable<String>> header in content.Headers )
            {
                clone.Headers.Add( header.Key, header.Value );
            }

            return clone;
        }

        #endregion
    }
}
