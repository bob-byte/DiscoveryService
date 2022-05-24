using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

using AutoMapper;

using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Messages;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Models;

namespace LUC.DiscoveryServices
{
    /// <summary>
    ///   Allows sending and receiving datagrams over multicast sockets.
    ///
    ///    Also listens on TCP/IP port for receiving side to establish connection.
    /// </summary>
    class Listeners : AbstractDsData, IDisposable
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
        private readonly IMapper m_mapper;


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
            AppSettings.AddNewMap<UdpReceiveResult, UdpMessageEventArgs>();
            m_mapper = AppSettings.Mapper;

            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;

            RunningIpAddresses = new List<IPAddress>();

            m_udpReceivers = new List<UdpClient>();
            m_tcpServers = new List<TcpServer>();

            if ( UseIpv4 )
            {
                ConfigureListeners( AddressFamily.InterNetwork, runningIpAddresses );
            }

            if ( UseIpv6 )
            {
                ConfigureListeners( AddressFamily.InterNetworkV6, runningIpAddresses );
            }

            foreach ( UdpClient r in m_udpReceivers )
            {
                ListenUdp( r );

                if(r.Client.IsBound)
                {
                    DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Successfully started listen UDP messages on {r.Client.LocalEndPoint} by the {nameof( UdpClient )}" );
                }
                else
                {
                    DsLoggerSet.DefaultLogger.LogInfo( $"Tried to listen UDP messages on {r.Client.LocalEndPoint} by the {nameof( UdpClient )}, but it is not bound" );
                }
            }

            // TODO: add SSL support
            foreach ( TcpServer tcpServer in m_tcpServers )
            {
                try
                {
                    tcpServer.Start();
                }
                catch ( SocketException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( message: $"Failed to run {nameof( TcpServer )}", ex );
                    continue;
                }
                catch ( SecurityException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( ex );
                }

                ListenTcp( tcpServer );
                if(tcpServer.IsSocketBound)
                {
                    DsLoggerSet.DefaultLogger.LogInfo( $"Successfully started listen TCP messages on {tcpServer.Endpoint} by the {nameof( TcpServer )}" );
                }
                else
                {
                    DsLoggerSet.DefaultLogger.LogInfo( $"Tried to listen TCP messages on {tcpServer.Endpoint} by the {nameof( TcpServer )}, but it is not bound to this {nameof(tcpServer.Endpoint)}" );
                }
            }
        }

        public ICollection<IPAddress> RunningIpAddresses { get; }

        private void ConfigureListeners( AddressFamily addressFamily, IEnumerable<IPAddress> runningIpAddresses )
        {
            SocketOptionLevel socketOptionLevel;
            IPAddress address;
            Func<IPAddress, Object> createdMulticastOption;

            switch ( addressFamily )
            {
                case AddressFamily.InterNetwork:
                {
                    address = IPAddress.Any;
                    socketOptionLevel = SocketOptionLevel.IP;
                    createdMulticastOption = ip => new MulticastOption( DsConstants.MulticastAddressIpv4, ip );

                    break;
                }

                case AddressFamily.InterNetworkV6:
                {
                    address = IPAddress.IPv6Any;
                    socketOptionLevel = SocketOptionLevel.IPv6;
                    createdMulticastOption = ip => new IPv6MulticastOption( DsConstants.MulticastAddressIpv6, ip.ScopeId );

                    break;
                }

                default:
                {
                    throw new NotSupportedException( message: $"Address family {addressFamily}." );
                }
            }

            var tcpServer = new TcpServer( address, RunningTcpPort )
            {
                OptionReuseAddress = true
            };
            m_tcpServers.Add( tcpServer );

            UdpClient udpReceiver = null;
            try
            {
                udpReceiver = new UdpClient( addressFamily );
                udpReceiver.Client.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true );
                udpReceiver.Client.Bind( localEP: new IPEndPoint( address, RunningUdpPort ) );
                udpReceiver.Client.ReceiveTimeout = (Int32)DsConstants.ReceiveTimeout.TotalMilliseconds;

                m_udpReceivers.Add( udpReceiver );
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
                foreach ( IPAddress ipAddress in runningIpAddresses.Where( ip => ip.AddressFamily == addressFamily ) )
                {
                    try
                    {
                        Object optionValue = createdMulticastOption( ipAddress );
                        udpReceiver.Client.SetSocketOption( socketOptionLevel, SocketOptionName.AddMembership, optionValue );

                        RunningIpAddresses.Add( ipAddress );
                    }
                    catch ( SocketException )
                    {
                        ;//do nothing
                    }
                }
            }
        }

        /// <summary>
        /// Listens for UDP messages asynchronously
        /// </summary>
        /// <param name="receiver">
        /// Object which returns data of the messages
        /// </param>
        //TODO: refactor it
        private void ListenUdp( UdpClient receiver ) =>
            // ReceiveAsync does not support cancellation.  So the receiver is disposed
            // to stop it. See https://github.com/dotnet/corefx/issues/9848
            _ = Task.Run( async () =>
             {

                 try
                 {
                     Task<UdpReceiveResult> task = receiver.ReceiveAsync();

                     _ = task.ContinueWith( x => ListenUdp( receiver ), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                     _ = task.ContinueWith( async taskReceiving =>
                     {
                         UdpReceiveResult udpReceiveResult;
                         try
                         {
                             udpReceiveResult = await taskReceiving.ConfigureAwait( continueOnCapturedContext: false );
                         }
                         catch ( Exception ex )
                         {
                             DsLoggerSet.DefaultLogger.LogFatal( message: $"Received exception during getting UDP message:\n{ex}" );
                             return;
                         }

                         UdpMessageEventArgs eventArgs = m_mapper.Map<UdpMessageEventArgs>( udpReceiveResult );

                         UdpMessageReceived?.Invoke( receiver, eventArgs );
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

        /// <summary>
        /// Listens for TCP messages asynchronously
        /// </summary>
        /// <param name="receiver">
        /// Object which returns data of the messages
        /// </param>
        private void ListenTcp( TcpServer tcpServer ) =>
            _ = Task.Run( async () =>
             {
                 try
                 {
                     Task<TcpSession> task = tcpServer.SessionWithMessageAsync();

                    //ListenTcp call is here not to stop listening when we received TCP message
                    _ = task.ContinueWith( x => ListenTcp( tcpServer ), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                    //using tasks provides unblocking event calls
                    _ = task.ContinueWith( async taskReceivingSession =>
                     {
                         TcpMessageEventArgs eventArgs = null;
                         TcpSession tcpSession = await taskReceivingSession.ConfigureAwait( continueOnCapturedContext: false );

                         try
                         {
                             eventArgs = await tcpServer.ReceiveAsync( DsConstants.ReceiveTimeout, tcpSession ).ConfigureAwait( false );
                         }
                         finally
                         {
                             tcpSession?.CanBeUsedByAnotherThread.Set();
                         }

                         if ( ( eventArgs != null ) && ( eventArgs.Buffer.Length > Message.MIN_LENGTH ) )
                         {
                             TcpMessageReceived?.Invoke( tcpServer, eventArgs );
                         }
                     }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously );

                     await task.ConfigureAwait( false );
                 }
                 catch ( ObjectDisposedException )
                 {
                    //if tcpServer is disposed, this value will be false
                    if ( tcpServer.IsStarted )
                     {
                         ListenTcp( tcpServer );
                     }
                 }
                //An error occurred when accessing the socket.
                catch ( SocketException ex )
                 {
                    //TODO Change absolutely TCP port (in TcpListener), but take into account maxValueTcpPort
                    DsLoggerSet.DefaultLogger.LogFatal( $"Failed to listen on TCP port.\n" +
                          $"{ex}" );

                     ListenTcp( tcpServer );
                 }
                //message is not from IPEndpoint
                catch ( InvalidOperationException ex )
                 {
                     DsLoggerSet.DefaultLogger.LogFatal( $"Failed to listen on TCP port.\n" +
                          $"{ex.Message}" );

                     ListenTcp( tcpServer );
                 }
                //all another exception is wrapped in TimeoutException
                catch ( TimeoutException )
                 {
                     ListenTcp( tcpServer );
                 }
#if DEBUG
                 catch ( Exception ex )
                 {
                     DsLoggerSet.DefaultLogger.LogCriticalError( message: $"Unhandled exception during listening TCP messages, {ex.GetType().Name}: {ex.Message}", ex );
                 }
#endif
             } );

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

                m_disposedValue = true;
            }
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
