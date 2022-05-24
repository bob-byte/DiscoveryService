using AutoMapper;

using DiscoveryServices.Common;
using DiscoveryServices.Interfaces;
using DiscoveryServices.Messages;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace DiscoveryServices
{
    internal class UdpListenersCollection : ListenersCollection<UdpMessageEventArgs, UdpClient>
    {
        private readonly IMapper m_mapper;

        /// <summary>
        ///   Creates a new instance of the <see cref="UdpListenersCollection"/> class.
        /// </summary>
        /// <param name="useIpv4">
        /// Send and receive on IPv4.
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        /// </param>
        /// <param name="useIpv6">
        /// Send and receive on IPv6.
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        /// </param>
        /// <param name="reachableIpAddresses">
        /// NetworkInterfaces wherefrom we should send to
        /// </param>
        public UdpListenersCollection( Boolean useIpv4, Boolean useIpv6, IEnumerable<IPAddress> reachableIpAddresses, Int32 listeningPort )
            : base( useIpv4, useIpv6, reachableIpAddresses, listeningPort )
        {
            AppSettings.AddNewMap<UdpReceiveResult, UdpMessageEventArgs>();
            m_mapper = AppSettings.Mapper;

            ConfigureListeners( m_reachableIpAddresses );
        }

        public override void StartMessagesReceiving()
        {
            if ( !DisposedValue )
            {
                if ( !m_isListening )
                {
                    foreach ( UdpClient udpListener in Listeners )
                    {
                        StartNextMessageReceiving( udpListener );

                        if ( udpListener.Client.IsBound )
                        {
                            DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Successfully started listen UDP messages on {udpListener.Client.LocalEndPoint} by the {nameof( UdpClient )}" );
                        }
                        else
                        {
                            DsLoggerSet.DefaultLogger.LogFatal( message: $"Tried to listen UDP messages on {udpListener.Client.LocalEndPoint} by the {nameof( UdpClient )}, but it is not bound" );
                        }
                    }

                    m_isListening = true;
                }
            }
            else
            {
                throw new ObjectDisposedException( GetType().Name );
            }
        }

        /// <summary>
        /// Listens for UDP messages asynchronously
        /// </summary>
        /// <param name="listener">
        /// Object which returns data of the messages
        /// </param>
        protected override void StartNextMessageReceiving( UdpClient listener )
        {
            // ReceiveAsync does not support cancellation.  So the receiver is disposed
            // to stop it. See https://github.com/dotnet/corefx/issues/9848
            Task.Run( async () =>
            {
                try
                {
                    Task<UdpReceiveResult> task = listener.ReceiveAsync();

                    _ = task.ContinueWith( x => StartNextMessageReceiving( listener ), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                    _ = task.ContinueWith( async taskReceiving =>
                    {
                        UdpReceiveResult udpReceiveResult = await taskReceiving.ConfigureAwait( continueOnCapturedContext: false );
                        UdpMessageEventArgs eventArgs = m_mapper.Map<UdpMessageEventArgs>( udpReceiveResult );

                        InvokeMessageReceived( listener, eventArgs );
                    }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                    await task.ConfigureAwait( false );
                }
                //receiver is disposed
                catch ( ObjectDisposedException )
                {
                    ;//do nothing
                }
                //Listeners object is null
                catch ( NullReferenceException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( message: $"Failed to listen UDP message, {ex.GetType().Name}: {ex.Message}", ex );
                }
                //An error occurred when accessing the socket.
                catch ( SocketException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( message: $"Failed to listen UDP message, {ex.GetType().Name}: {ex.Message}", ex );
                }
                //timeout to read package
                catch ( TimeoutException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( message: $"Failed to listen UDP message, {ex.GetType().Name}: {ex.Message}", ex );
                }
#if DEBUG
                catch ( Exception ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( message: $"Unhandled exception during listening UDP messages, {ex.GetType().Name}: {ex.Message}", ex );
                }
#endif
            } );
        }

        private void ConfigureListeners( AddressFamily addressFamily, IEnumerable<IPAddress> reachableIpAddresses )
        {
            SocketOptionLevel socketOptionLevel;
            IPAddress listeningAddress;
            Func<IPAddress, Object> createdMulticastOption;

            switch ( addressFamily )
            {
                case AddressFamily.InterNetwork:
                {
                    listeningAddress = IPAddress.Any;//0.0.0.0
                    socketOptionLevel = SocketOptionLevel.IP;
                    createdMulticastOption = ip =>
                        new MulticastOption( DsConstants.MulticastAddressIpv4, ip );

                    break;
                }

                case AddressFamily.InterNetworkV6:
                {
                    listeningAddress = IPAddress.IPv6Any;//::ffff:0:0
                    socketOptionLevel = SocketOptionLevel.IPv6;
                    createdMulticastOption = ip =>
                        new IPv6MulticastOption( DsConstants.MulticastAddressIpv6, ip.ScopeId );

                    break;
                }

                default:
                {
                    throw new NotSupportedException( message: $"Address family {addressFamily}." );
                }
            }

            UdpClient udpReceiver = null;
            try
            {
                udpReceiver = new UdpClient( addressFamily );
                udpReceiver.Client.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true );
                udpReceiver.Client.Bind( localEP: new IPEndPoint( listeningAddress, ListeningPort ) );

                Listeners.Add( udpReceiver );
            }
            catch ( SocketException ex )
            {
                DsLoggerSet.DefaultLogger.LogCriticalError( ex );
            }
            catch ( SecurityException ex )
            {
                DsLoggerSet.DefaultLogger.LogCriticalError( ex );
            }

            if ( udpReceiver != null )
            {
                foreach ( IPAddress ipAddress in reachableIpAddresses.Where( ip => ip.AddressFamily == addressFamily ) )
                {
                    try
                    {
                        Object optionValue = createdMulticastOption( ipAddress );
                        udpReceiver.Client.SetSocketOption( socketOptionLevel, SocketOptionName.AddMembership, optionValue );
                    }
                    catch ( SocketException )
                    {
                        DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Cannot join socket with IP-address {ipAddress} to multicast group" );
                    }
                }
            }
        }

        private void ConfigureListeners( IEnumerable<IPAddress> reachableIpAddresses )
        {
            foreach ( IPAddress listeningIpAddress in reachableIpAddresses )
            {
                try
                {
                    var udpReceiver = new UdpClient( listeningIpAddress.AddressFamily );
                    udpReceiver.Client.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true );
                    udpReceiver.Client.Bind( localEP: new IPEndPoint( listeningIpAddress, ListeningPort ) );

                    Object multicastOption;
                    SocketOptionLevel socketOptionLevel;
                    switch ( listeningIpAddress.AddressFamily )
                    {
                        case AddressFamily.InterNetwork:
                        {
                            multicastOption = new MulticastOption( 
                                DsConstants.MulticastAddressIpv4, 
                                listeningIpAddress 
                            );
                            socketOptionLevel = SocketOptionLevel.IP;

                            break;
                        }

                        case AddressFamily.InterNetworkV6:
                        {
                            multicastOption = new IPv6MulticastOption( 
                                DsConstants.MulticastAddressIpv6, 
                                listeningIpAddress.ScopeId 
                            );
                            socketOptionLevel = SocketOptionLevel.IPv6;

                            break;
                        }

                        default:
                        {
                            //only IPv4 and IPv6 is supported by UDP
                            continue;
                        }
                    }

                    udpReceiver.Client.SetSocketOption( socketOptionLevel, SocketOptionName.AddMembership, multicastOption );

                    Listeners.Add( udpReceiver );
                }
                catch ( SocketException )
                {
                    DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Cannot join socket with IP-address {listeningIpAddress} to multicast group" );
                }
            }
        }
    }
}
