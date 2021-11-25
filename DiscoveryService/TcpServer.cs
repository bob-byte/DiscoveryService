using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Extensions;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages;
using LUC.Interfaces;
using LUC.Services.Implementation;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices
{
    /// <summary>
    /// TCP server is used to connect, disconnect and manage TCP sessions
    /// </summary>
    /// <remarks>Thread-safe</remarks>
    class TcpServer : IDisposable
    {
        private const Int32 MAX_SESSIONS_COUNT = 10000;

        private readonly static ILoggingService log;

        private readonly TimeSpan m_waitForCheckingWaitingSocket = TimeSpan.FromSeconds( 0.5 );

        static TcpServer()
        {
            log = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        /// <summary>
        /// Initialize TCP server with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public TcpServer( IPAddress address, Int32 port ) : this( new IPEndPoint( address, port ) ) { }
        /// <summary>
        /// Initialize TCP server with a given IP address and port number
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port number</param>
        public TcpServer( String address, Int32 port ) : this( new IPEndPoint( IPAddress.Parse( address ), port ) ) { }
        /// <summary>
        /// Initialize TCP server with a given IP endpoint
        /// </summary>
        /// <param name="endpoint">IP endpoint</param>
        public TcpServer( IPEndPoint endpoint )
        {
            Id = Guid.NewGuid();
            Endpoint = endpoint;
        }

        /// <summary>
        /// Server Id
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// IP endpoint
        /// </summary>
        public IPEndPoint Endpoint { get; private set; }

        /// <summary>
        /// Number of sessions connected to the server
        /// </summary>
        public Int64 ConnectedSessions => Sessions.Count;
        /// <summary>
        /// Number of bytes pending sent by the server
        /// </summary>
        public Int64 BytesPending => _bytesPending;
        /// <summary>
        /// Number of bytes sent by the server
        /// </summary>
        public Int64 BytesSent => _bytesSent;
        /// <summary>
        /// Number of bytes received by the server
        /// </summary>
        public Int64 BytesReceived => _bytesReceived;

        /// <summary>
        /// Option: acceptor backlog size
        /// </summary>
        /// <remarks>
        /// This option will set the listening socket's backlog size
        /// </remarks>
        public Int32 OptionAcceptorBacklog { get; set; } = 10;
        /// <summary>
        /// Option: dual mode socket
        /// </summary>
        /// <remarks>
        /// Specifies whether the Socket is a dual-mode socket used for both IPv4 and IPv6.
        /// Will work only if socket is bound on IPv6 address.
        /// </remarks>
        public Boolean OptionDualMode { get; set; }
        /// <summary>
        /// Option: keep alive
        /// </summary>
        /// <remarks>
        /// This option will setup SO_KEEPALIVE if the OS support this feature
        /// </remarks>
        public Boolean OptionKeepAlive { get; set; }
        /// <summary>
        /// Option: no delay
        /// </summary>
        /// <remarks>
        /// This option will enable/disable Nagle's algorithm for TCP protocol
        /// </remarks>
        public Boolean OptionNoDelay { get; set; }
        /// <summary>
        /// Option: reuse address
        /// </summary>
        /// <remarks>
        /// This option will enable/disable SO_REUSEADDR if the OS support this feature
        /// </remarks>
        public Boolean OptionReuseAddress { get; set; }
        /// <summary>
        /// Option: enables a socket to be bound for exclusive access
        /// </summary>
        /// <remarks>
        /// This option will enable/disable SO_EXCLUSIVEADDRUSE if the OS support this feature
        /// </remarks>
        public Boolean OptionExclusiveAddressUse { get; set; }
        /// <summary>
        /// Option: receive buffer size
        /// </summary>
        public Int32 OptionReceiveBufferSize { get; set; } = 8192;
        /// <summary>
        /// Option: send buffer size
        /// </summary>
        public Int32 OptionSendBufferSize { get; set; } = 8192;

        #region Start/Stop server

        // Server acceptor
        private Socket _acceptorSocket;
        private SocketAsyncEventArgs _acceptorEventArg;

        // Server statistic
        internal Int64 _bytesPending;
        internal Int64 _bytesSent;
        internal Int64 _bytesReceived;

        /// <summary>
        /// Is the server started?
        /// </summary>
        public Boolean IsStarted { get; private set; }
        /// <summary>
        /// Is the server accepting new clients?
        /// </summary>
        public Boolean IsAccepting { get; private set; }

        /// <summary>
        /// Create a new socket object
        /// </summary>
        /// <remarks>
        /// Method may be override if you need to prepare some specific socket object in your implementation.
        /// </remarks>
        /// <returns>Socket object</returns>
        protected virtual Socket CreateSocket() => new Socket( Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp );

        /// <summary>
        /// Start the server
        /// </summary>
        /// <returns>'true' if the server was successfully started, 'false' if the server failed to start</returns>
        public virtual Boolean Start()
        {
            if ( IsStarted )
                return false;

            // Setup acceptor event arg
            _acceptorEventArg = new SocketAsyncEventArgs();
            _acceptorEventArg.Completed += OnAsyncCompleted;

            // Create a new acceptor socket
            _acceptorSocket = CreateSocket();

            // Update the acceptor socket disposed flag
            IsSocketDisposed = false;

            // Apply the option: reuse address
            _acceptorSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, OptionReuseAddress );
            // Apply the option: exclusive address use
            _acceptorSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, OptionExclusiveAddressUse );
            // Apply the option: dual mode (this option must be applied before listening)
            if ( _acceptorSocket.AddressFamily == AddressFamily.InterNetworkV6 )
                _acceptorSocket.DualMode = OptionDualMode;

            // Bind the acceptor socket to the IP endpoint
            _acceptorSocket.Bind( Endpoint );
            // Refresh the endpoint property based on the actual endpoint created
            Endpoint = (IPEndPoint)_acceptorSocket.LocalEndPoint;

            // Call the server starting handler
            OnStarting();

            // Start listen to the acceptor socket with the given accepting backlog size
            _acceptorSocket.Listen( OptionAcceptorBacklog );

            // Reset statistic
            _bytesPending = 0;
            _bytesSent = 0;
            _bytesReceived = 0;

            // Update the started flag
            IsStarted = true;

            // Call the server started handler
            OnStarted();

            // Perform the first server accept
            IsAccepting = true;
            StartAccept( _acceptorEventArg );

            return true;
        }

        /// <summary>
        /// Stop the server
        /// </summary>
        /// <returns>'true' if the server was successfully stopped, 'false' if the server is already stopped</returns>
        public virtual Boolean Stop()
        {
            if ( !IsStarted )
                return false;

            // Stop accepting new clients
            IsAccepting = false;

            // Reset acceptor event arg
            _acceptorEventArg.Completed -= OnAsyncCompleted;

            // Call the server stopping handler
            OnStopping();

            try
            {
                // Close the acceptor socket
                _acceptorSocket.Close();

                // Dispose the acceptor socket
                _acceptorSocket.Dispose();

                // Dispose event arguments
                _acceptorEventArg.Dispose();

                // Update the acceptor socket disposed flag
                IsSocketDisposed = true;
            }
            catch ( ObjectDisposedException ) { }

            // Disconnect all sessions
            DisconnectAll();

            // Update the started flag
            IsStarted = false;

            // Call the server stopped handler
            OnStopped();

            return true;
        }

        /// <summary>
        /// Restart the server
        /// </summary>
        /// <returns>'true' if the server was successfully restarted, 'false' if the server failed to restart</returns>
        public virtual Boolean Restart()
        {
            if ( !Stop() )
                return false;

            while ( IsStarted )
                Thread.Yield();

            return Start();
        }

        #endregion

        #region Accepting clients

        /// <summary>
        /// Start accept a new client connection
        /// </summary>
        private void StartAccept( SocketAsyncEventArgs e )
        {
            // Socket must be cleared since the context object is being reused
            e.AcceptSocket = null;

            // Async accept a new client connection
            if ( !_acceptorSocket.AcceptAsync( e ) )
                ProcessAccept( e );
        }

        /// <summary>
        /// Process accepted client connection
        /// </summary>
        private void ProcessAccept( SocketAsyncEventArgs e )
        {
            if ( e.SocketError == SocketError.Success )
            {
                if ( MAX_SESSIONS_COUNT <= Sessions.Count + 1 )
                {
                    UnregisterSession();
                }

                TcpSession session = CreateSession();

                // Register the session
                RegisterSession( session );

                // Connect new session
                session.Connect( e.AcceptSocket );
            }
            else
                SendError( e.SocketError );

            // Accept the next client connection
            if ( IsAccepting )
                StartAccept( e );
        }

        public async Task<TcpMessageEventArgs> ReceiveAsync( TimeSpan timeoutToRead )
        {
            IPEndPoint ipEndPoint;
            TcpSession clientToReadMessage;
            Byte[] readBytes;
            CancellationTokenSource cancelSource = new CancellationTokenSource();

            try
            {
                clientToReadMessage = await SessionWithNewDataAsync().ConfigureAwait(continueOnCapturedContext: false);

                AutoResetEvent receiveDone = new AutoResetEvent( initialState: false );

                ConfiguredTaskAwaitable<Byte[]> taskReadBytes = clientToReadMessage.Socket.ReadAllAvailableBytesAsync( receiveDone, Constants.MAX_CHUNK_READ_PER_ONE_TIME, Constants.MAX_AVAILABLE_READ_BYTES, cancelSource.Token ).ConfigureAwait( false );

                Boolean isReceivedInTime = receiveDone.WaitOne( timeoutToRead );
                cancelSource.Cancel();

                if ( isReceivedInTime )
                {
                    readBytes = await taskReadBytes;
                    ipEndPoint = clientToReadMessage.Socket.RemoteEndPoint as IPEndPoint;

                }
                else
                {
                    throw new TimeoutException( $"Timeout to read data from {clientToReadMessage.Socket.RemoteEndPoint}" );
                }
            }
            catch ( ObjectDisposedException )
            {
                throw;
            }
            catch ( SocketException )
            {
                if ( IsSocketDisposed )
                {
                    throw new ObjectDisposedException( $"Socket {_acceptorSocket.LocalEndPoint} is disposed" );
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                cancelSource.Dispose();
            }

            TcpMessageEventArgs receiveResult = new TcpMessageEventArgs();
            if ( ipEndPoint != null )
            {
                receiveResult.Buffer = readBytes;
                receiveResult.RemoteEndPoint = ipEndPoint;
                receiveResult.AcceptedSocket = clientToReadMessage.Socket;
                receiveResult.LocalEndPoint = _acceptorSocket.LocalEndPoint;
            }
            else
            {
                throw new InvalidOperationException( "Cannot convert remote end point to IPEndPoint" );
            }

            return receiveResult;
        }

        public async Task<TcpSession> SessionWithNewDataAsync()
        {
            TcpSession sessionWithData = null;

            while ( sessionWithData == null )
            {
                sessionWithData = Sessions.LastOrDefault( c => c.Value.Socket?.Available > 0 ).Value;

                if ( sessionWithData == null )
                {
                    await Task.Delay( m_waitForCheckingWaitingSocket );
                }
            }

            return sessionWithData;
        }

        /// <summary>
        /// This method is the callback method associated with Socket.AcceptAsync()
        /// operations and is invoked when an accept operation is complete
        /// </summary>
        private void OnAsyncCompleted( Object sender, SocketAsyncEventArgs e )
        {
            if ( IsSocketDisposed )
                return;

            ProcessAccept( e );
        }

        #endregion

        #region Session factory

        /// <summary>
        /// Create TCP session factory method
        /// </summary>
        /// <returns>TCP session</returns>
        protected virtual TcpSession CreateSession() => new TcpSession( this );

        #endregion

        #region Session management

        // Server sessions
        protected readonly ConcurrentDictionary<Guid, TcpSession> Sessions = new ConcurrentDictionary<Guid, TcpSession>();

        /// <summary>
        /// Disconnect all connected sessions
        /// </summary>
        /// <returns>'true' if all sessions were successfully disconnected, 'false' if the server is not started</returns>
        public virtual Boolean DisconnectAll()
        {
            if ( !IsStarted )
                return false;

            // Disconnect all sessions
            foreach ( TcpSession session in Sessions.Values )
                session.Disconnect();

            return true;
        }

        /// <summary>
        /// Find a session with a given Id
        /// </summary>
        /// <param name="id">Session Id</param>
        /// <returns>Session with a given Id or null if the session it not connected</returns>
        public TcpSession FindSession( Guid id ) =>
            // Try to find the required session
            Sessions.TryGetValue( id, out TcpSession result ) ? result : null;

        /// <summary>
        /// Register a new session
        /// </summary>
        /// <param name="session">Session to register</param>
        internal void RegisterSession( TcpSession session ) =>
            // Register a new session
            Sessions.TryAdd( session.Id, session );

        /// <summary>
        /// Unregister the oldest session
        /// </summary>
        internal void UnregisterSession()
        {
            TcpSession oldestSession = Sessions.FirstOrDefault( c => ( c.Value.Socket == null ) || !( c.Value.Socket.Connected ) ).Value;

            if ( oldestSession == null )
            {
                oldestSession = Sessions.FirstOrDefault( c => c.Value.Socket?.Available == 0 ).Value;

                if ( oldestSession == null )
                {
                    oldestSession = Sessions.FirstOrDefault().Value;
                }
            }

            if ( Sessions.Count != 0 )
            {
                UnregisterSession( oldestSession.Id );
            }
        }

        /// <summary>
        /// Unregister session by Id
        /// </summary>
        /// <param name="id">Session Id</param>
        internal void UnregisterSession( Guid id ) =>
            // Unregister session by Id
            Sessions.TryRemove( id, out TcpSession temp );

        #endregion

        #region Multicasting

        /// <summary>
        /// Multicast data to all connected sessions
        /// </summary>
        /// <param name="buffer">Buffer to multicast</param>
        /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
        public virtual Boolean Multicast( Byte[] buffer ) => Multicast( buffer, 0, buffer.Length );

        /// <summary>
        /// Multicast data to all connected clients
        /// </summary>
        /// <param name="buffer">Buffer to multicast</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
        public virtual Boolean Multicast( Byte[] buffer, Int64 offset, Int64 size )
        {
            if ( !IsStarted )
                return false;

            if ( size == 0 )
                return true;

            // Multicast data to all sessions
            foreach ( TcpSession session in Sessions.Values )
                session.SendAsync( buffer, offset, size );

            return true;
        }

        /// <summary>
        /// Multicast text to all connected clients
        /// </summary>
        /// <param name="text">Text string to multicast</param>
        /// <returns>'true' if the text was successfully multicasted, 'false' if the text was not multicasted</returns>
        public virtual Boolean Multicast( String text ) => Multicast( Encoding.UTF8.GetBytes( text ) );

        #endregion

        #region Server handlers

        /// <summary>
        /// Handle server starting notification
        /// </summary>
        protected virtual void OnStarting() { }
        /// <summary>
        /// Handle server started notification
        /// </summary>
        protected virtual void OnStarted() { }
        /// <summary>
        /// Handle server stopping notification
        /// </summary>
        protected virtual void OnStopping() { }
        /// <summary>
        /// Handle server stopped notification
        /// </summary>
        protected virtual void OnStopped() { }

        /// <summary>
        /// Handle session connecting notification
        /// </summary>
        /// <param name="session">Connecting session</param>
        protected virtual void OnConnecting( TcpSession session ) { }
        /// <summary>
        /// Handle session connected notification
        /// </summary>
        /// <param name="session">Connected session</param>
        protected virtual void OnConnected( TcpSession session ) { }
        /// <summary>
        /// Handle session disconnecting notification
        /// </summary>
        /// <param name="session">Disconnecting session</param>
        protected virtual void OnDisconnecting( TcpSession session ) { }
        /// <summary>
        /// Handle session disconnected notification
        /// </summary>
        /// <param name="session">Disconnected session</param>
        protected virtual void OnDisconnected( TcpSession session ) { }

        /// <summary>
        /// Handle error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        protected virtual void OnError( SocketError error ) { }

        internal void OnConnectingInternal( TcpSession session ) => OnConnecting( session );
        internal void OnConnectedInternal( TcpSession session ) => OnConnected( session );
        internal void OnDisconnectingInternal( TcpSession session ) => OnDisconnecting( session );
        internal void OnDisconnectedInternal( TcpSession session ) => OnDisconnected( session );

        #endregion

        #region Error handling

        /// <summary>
        /// Send error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        private void SendError( SocketError error )
        {
            // Skip disconnect errors
            if ( ( error == SocketError.ConnectionAborted ) ||
                ( error == SocketError.ConnectionRefused ) ||
                ( error == SocketError.ConnectionReset ) ||
                ( error == SocketError.OperationAborted ) ||
                ( error == SocketError.Shutdown ) )
                return;

            OnError( error );
        }

        #endregion

        #region IDisposable implementation

        /// <summary>
        /// Disposed flag
        /// </summary>
        public Boolean IsDisposed { get; private set; }

        /// <summary>
        /// Acceptor socket disposed flag
        /// </summary>
        public Boolean IsSocketDisposed { get; private set; } = true;

        // Implement IDisposable.
        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( Boolean disposingManagedResources )
        {
            // The idea here is that Dispose(Boolean) knows whether it is
            // being called to do explicit cleanup (the Boolean is true)
            // versus being called due to a garbage collection (the Boolean
            // is false). This distinction is useful because, when being
            // disposed explicitly, the Dispose(Boolean) method can safely
            // execute code using reference type fields that refer to other
            // objects knowing for sure that these other objects have not been
            // finalized or disposed of yet. When the Boolean is false,
            // the Dispose(Boolean) method should not execute code that
            // refer to reference type fields because those objects may
            // have already been finalized."

            if ( !IsDisposed )
            {
                if ( disposingManagedResources )
                {
                    // Dispose managed resources here...
                    Stop();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...

                // Mark as disposed.
                IsDisposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        ~TcpServer()
        {
            // Simply call Dispose(false).
            Dispose( false );
        }

        #endregion
    }
}