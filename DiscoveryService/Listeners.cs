using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Threading.Tasks;

using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Allows sending and receiving datagrams over multicast sockets.
    ///
    ///    Also listens on TCP/IP port for receiving side to establish connection.
    /// </summary>
    class Listeners : AbstractService, IDisposable
    {
        /// <summary>
        /// It calls method OnUdpMessage, which run SendTcp,
        /// in order to connect back to the host, that sends muticast
        /// </summary>
        public event EventHandler<UdpMessageEventArgs> UdpMessageReceived;

        /// <summary>
        /// It calls method OnTcpMessage, which add new groups to ServiceDiscovery.GroupsSupported
        /// </summary>
        public event EventHandler<TcpMessageEventArgs> TcpMessageReceived;

        private readonly List<UdpClient> m_udpReceivers;
        private readonly ICollection<TcpServer> m_tcpServers;

        /// <summary>
        ///   Creates a new instance of the <see cref="Listeners"/> class.
        /// </summary>
        /// <param name="profile">
        /// Info about current peer
        /// </param>
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
        /// <param name="runningIpAddresses">
        /// NetworkInterfaces wherefrom we should send to
        /// </param>
        public Listeners( Boolean useIpv4, Boolean useIpv6, ICollection<IPAddress> runningIpAddresses )
        {
            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;

            m_udpReceivers = new List<UdpClient>();
            m_tcpServers = new List<TcpServer>();
            UdpClient udpReceiver4 = null;

            if ( UseIpv4 )
            {
                udpReceiver4 = new UdpClient( AddressFamily.InterNetwork );
                udpReceiver4.Client.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true );
                udpReceiver4.Client.Bind( new IPEndPoint( IPAddress.Any, (Int32)RunningUdpPort ) );
                m_udpReceivers.Add( udpReceiver4 );
            }

            UdpClient udpReceiver6 = null;
            if ( UseIpv6 )
            {
                udpReceiver6 = new UdpClient( AddressFamily.InterNetworkV6 );
                udpReceiver6.Client.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true );
                udpReceiver6.Client.Bind( new IPEndPoint( IPAddress.IPv6Any, (Int32)RunningUdpPort ) );
                m_udpReceivers.Add( udpReceiver6 );
            }

            RunningIpAddresses = runningIpAddresses;
            foreach ( IPAddress address in runningIpAddresses )
            {
                TcpServer tcpServer = new TcpServer( address, RunningTcpPort );
                m_tcpServers.Add( tcpServer );

                try
                {
                    switch ( address.AddressFamily )
                    {
                        case AddressFamily.InterNetwork:
                        {
                            udpReceiver4.Client.SetSocketOption( SocketOptionLevel.IP, SocketOptionName.AddMembership, optionValue: new MulticastOption( Constants.MulticastAddressIp4, address ) );

                            break;
                        }

                        case AddressFamily.InterNetworkV6:
                        {
                            udpReceiver6.Client.SetSocketOption( SocketOptionLevel.IPv6, SocketOptionName.AddMembership, optionValue: new IPv6MulticastOption( Constants.MulticastAddressIp6, address.ScopeId ) );

                            break;
                        }

                        default:
                        {
                            throw new NotSupportedException( $"Address family {address.AddressFamily}." );
                        }
                    }
                }
                catch (SocketException)
                {
                    ;//do nothing
                }
                //in case if DS is stopped
                catch (ObjectDisposedException)
                {
                    ;//do nothing
                }
            }

            foreach ( UdpClient r in m_udpReceivers )
            {
                ListenUdp( r );
            }

            // TODO: add SSL support
            foreach ( TcpServer tcpReceiver in m_tcpServers )
            {
                try
                {
                    tcpReceiver.Start();
                }
                catch (SocketException ex)
                {
                    LoggingService.LogError( ex.Message );
                    continue;
                }

                ListenTcp( tcpReceiver );
            }
        }

        public ICollection<IPAddress> RunningIpAddresses { get; }

        internal static List<IPAddress> IpAddressesOfInterfaces( IEnumerable<NetworkInterface> nics, Boolean useIpv4, Boolean useIpv6 ) =>
            nics.SelectMany( NetworkInterfaceLocalAddresses )
                .Where( a => ( useIpv4 && a.AddressFamily == AddressFamily.InterNetwork )
                     || ( useIpv6 && a.AddressFamily == AddressFamily.InterNetworkV6 ) )
                .ToList();

        /// <summary>
        /// Listens for UDP messages asynchronously
        /// </summary>
        /// <param name="receiver">
        /// Object which returns data of the messages
        /// </param>
        private void ListenUdp( UdpClient receiver ) =>
            // ReceiveAsync does not support cancellation.  So the receiver is disposed
            // to stop it. See https://github.com/dotnet/corefx/issues/9848
            _ = Task.Run( async () =>
             {
                 try
                 {
                     Task<UdpReceiveResult> task = receiver.ReceiveAsync();

                     _ = task.ContinueWith( x => ListenUdp( receiver ), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                     _ = task.ContinueWith( ( taskReceiving ) =>
                     {
                         UdpMessageEventArgs eventArgs = new UdpMessageEventArgs
                         {
                             Buffer = taskReceiving.Result.Buffer,
                             RemoteEndPoint = taskReceiving.Result.RemoteEndPoint
                         };

                         UdpMessageReceived?.Invoke( receiver, eventArgs );
                         //await Task.Run(() => UdpMessageReceived.Invoke(receiver, eventArgs)).
                         //ConfigureAwait(continueOnCapturedContext: false);
                     }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                     await task.ConfigureAwait( false );
                 }
                 catch ( ObjectDisposedException )
                 {
                     return;
                 }
                 catch ( SocketException e )
                 {
                     LoggingService.LogError( $"Failed to listen UDP message, SocketException: {e.Message}" );
                     return;
                 }
             } );

        /// <summary>
        /// Listens for TCP messages asynchronously
        /// </summary>
        /// <param name="receiver">
        /// Object which returns data of the messages
        /// </param>
        private void ListenTcp( TcpServer tcpServer )
        {
            // ReceiveAsync does not support cancellation.  So the receiver is disposed
            // to stop it. See https://github.com/dotnet/corefx/issues/9848
            _ = Task.Run( async () =>
            {
                try
                {
                    Task<TcpMessageEventArgs> task = tcpServer.ReceiveAsync( Constants.ReceiveTimeout );

                    //ListenTcp call is here not to stop listening when we received TCP message
                    _ = task.ContinueWith( x => ListenTcp( tcpServer ), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                    //using tasks provides unblocking event calls
                    _ = task.ContinueWith( taskReceiving =>
                    {
                        TcpMessageEventArgs eventArgs = taskReceiving.Result;
                        LoggingService.LogInfo( $"Received {eventArgs.Buffer.Length} bytes" );

                        TcpMessageReceived?.Invoke( tcpServer, eventArgs );
                    }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                    await task.ConfigureAwait( continueOnCapturedContext: false );
                }
                catch ( ObjectDisposedException )
                {
                    return;
                }
                catch ( SocketException e )
                {
                    //TODO don't return. Change absolutely TCP port (in TcpListener), but take into account maxValueTcpPort
                    LoggingService.LogError( $"Failed to listen on TCP port.\n" +
                         $"{e}" );
                    return;
                }
                catch ( TimeoutException e )
                {
                    LoggingService.LogError( $"Failed to listen on TCP port.\n" +
                        $"{e}" );

                    ListenTcp( tcpServer );
                }
            } );
        }

        /// <param name="nic">
        /// <see cref="NetworkInterface"/> wherefrom you want to get collection of local <see cref="IPAddress"/>
        /// </param>
        /// <returns>
        /// Collection of local <see cref="IPAddress"/> according to <paramref name="nic"/>
        /// </returns>
        private static IEnumerable<IPAddress> NetworkInterfaceLocalAddresses( NetworkInterface nic ) => 
            nic.GetIPProperties()
               .UnicastAddresses
               .Select( x => x.Address )
               .Where( x => x.AddressFamily != AddressFamily.InterNetworkV6 || x.IsIPv6LinkLocal );

        #region IDisposable Support

        private Boolean m_disposedValue = false; // To detect redundant calls

        protected virtual void Dispose( Boolean disposing )
        {
            if ( !m_disposedValue )
            {
                UdpMessageReceived = null;
                TcpMessageReceived = null;

                foreach ( UdpClient receiver in m_udpReceivers )
                {
                    try
                    {
                        receiver.Dispose();
                    }
                    catch
                    {
                        // eat it.
                    }
                }
                m_udpReceivers.Clear();

                foreach ( TcpServer receiver in m_tcpServers )
                {
                    try
                    {
                        receiver.Dispose();
                    }
                    catch
                    {
                        // eat it.
                    }
                }
                m_tcpServers.Clear();

                GC.Collect();//maybe it should be deleted

                m_disposedValue = true;
            }
        }

        ~Listeners()
        {
            Dispose( false );
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        #endregion
    }
}
