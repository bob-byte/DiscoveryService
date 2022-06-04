using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Interfaces;
using LUC.DiscoveryServices.Messages;
using LUC.Interfaces.Constants;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices
{
    class TcpListenersCollection : ListenersCollection<TcpMessageEventArgs, TcpServer>
    {
        public TcpListenersCollection( Boolean useIpv4, Boolean useIpv6, IEnumerable<IPAddress> reachableIpAddresses, Int32 listeningPort )
            : base( useIpv4, useIpv6, reachableIpAddresses, listeningPort )
        {
            foreach ( IPAddress ipAddress in m_reachableIpAddresses )
            {
                AddDefaultNewTcpListener( endPointToBind: ipAddress, listeningPort );
            }
        }

        public override void StartMessagesReceiving()
        {
            if ( !DisposedValue )
            {
                if ( !m_isListening )
                {
                    // TODO: add SSL support
                    foreach ( TcpServer tcpListener in Listeners )
                    {
                        try
                        {
                            tcpListener.Start();
                        }
                        catch ( SocketException ex )
                        {
                            DsLoggerSet.DefaultLogger.LogCriticalError( message: $"Failed to run {nameof( TcpServer )}", ex );
                            continue;
                        }
                        catch ( SecurityException ex )
                        {
                            DsLoggerSet.DefaultLogger.LogCriticalError( $"Failed to run {nameof( TcpServer )}", ex );
                            continue;
                        }

                        StartNextMessageReceiving( tcpListener );
                        if ( tcpListener.IsSocketBound )
                        {
                            DsLoggerSet.DefaultLogger.LogInfo( $"Successfully started listen TCP messages on {tcpListener.Endpoint} by the {nameof( TcpServer )}" );
                        }
                        else
                        {
                            DsLoggerSet.DefaultLogger.LogInfo( $"Tried to listen TCP messages on {tcpListener.Endpoint} by the {nameof( TcpServer )}, but it is not bound to this {nameof( tcpListener.Endpoint )}" );
                        }
                    }

                    m_isListening = Listeners.All( c => c.IsStarted ) && ( Listeners.Count > 0 );
                }
            }
            else
            {
                throw new ObjectDisposedException( GetType().Name );
            }
        }

        protected override void StartNextMessageReceiving( TcpServer listener )
        {
            Task.Run( async () =>
            {
                try
                {
                    Task<TcpSession> task = listener.SessionWithMessageAsync();

                    //ListenTcp call is here not to stop listening when we received TCP message
                    _ = task.ContinueWith( x => StartNextMessageReceiving( listener ), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                    //using tasks provides unblocking event calls
                    _ = task.ContinueWith( async taskReceivingSession =>
                    {
                        TcpMessageEventArgs eventArgs = null;
                        TcpSession tcpSession = await taskReceivingSession.ConfigureAwait( continueOnCapturedContext: false );

                        try
                        {
                            eventArgs = await listener.ReceiveAsync( DsConstants.ReceiveTimeout, tcpSession ).ConfigureAwait( false );
                        }
                        finally
                        {
                            tcpSession?.CanBeUsedByAnotherThread.Set();
                        }
                        
                        InvokeMessageReceived( listener, eventArgs );
                    }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                    await task.ConfigureAwait( false );
                }
                catch ( ObjectDisposedException )
                {
                    //if tcpServer is disposed, this value will be false
                    if ( listener.IsStarted )
                    {
                        StartNextMessageReceiving( listener );
                    }
                }
#if DEBUG
                catch ( Exception ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( message: $"Unhandled exception during listening TCP messages, {ex.GetType().Name}: {ex.Message}", ex );
                    StartNextMessageReceiving(listener);
                }
#endif
            } );
        }

        private void AddDefaultNewTcpListener( IPAddress endPointToBind, Int32 listeningPort )
        {
            var tcpListener = new TcpServer( endPointToBind, listeningPort )
            {
                OptionReuseAddress = true
            };
            Listeners.Add( tcpListener );
        }
    }
}
