using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Extensions;
using LUC.DiscoveryServices.Messages;
using LUC.Interfaces.Constants;

using Nito.AsyncEx;

using System;
using System.Collections.Concurrent;
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

        private readonly TimeSpan m_waitForCheckingWaitingSocket;

        private readonly TimeSpan m_waitTakeSocket;

        private static readonly ParallelOptions s_parallelOptions;

        static TcpServer()
        {
            s_parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = DsConstants.MAX_THREADS
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
        private TcpServer( IPEndPoint endpoint )
        {
            Id = Guid.NewGuid();
            Endpoint = endpoint;

            m_waitForCheckingWaitingSocket = TimeSpan.FromSeconds( value: 0.5 );
            m_waitTakeSocket = TimeSpan.FromSeconds( 0.3 );
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
        public Int64 ConnectedSessions => m_sessions.Count;
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
        /// Option: enables a socket to be bound for exclusive access.
        /// </summary>
        /// <value>
        /// If value is <see langword="true"/> then no one can bind to <seealso cref="Endpoint"/>, 
        /// otherwise else sockets will be available to do it
        /// </value>
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
        private Socket m_acceptorSocket;
        private SocketAsyncEventArgs m_acceptorEventArg;

        // Server statistic
        internal Int64 _bytesPending;
        internal Int64 _bytesSent;
        internal Int64 _bytesReceived;

        /// <summary>
        /// Is the server started?
        /// </summary>
        public Boolean IsStarted { get; private set; }

        public Boolean IsSocketBound => ( m_acceptorSocket != null ) && m_acceptorSocket.IsBound;

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
            {
                return false;
            }

            // Setup acceptor event arg
            m_acceptorEventArg = new SocketAsyncEventArgs();
            m_acceptorEventArg.Completed += OnAsyncCompleted;

            // Create a new acceptor socket
            m_acceptorSocket = CreateSocket();

            // Update the acceptor socket disposed flag
            IsSocketDisposed = false;

            // Apply the option: reuse address
            m_acceptorSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, OptionReuseAddress );
            // Apply the option: exclusive address use
            m_acceptorSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, OptionExclusiveAddressUse );
            // Apply the option: dual mode (this option must be applied before listening)
            if ( m_acceptorSocket.AddressFamily == AddressFamily.InterNetworkV6 )
            {
                m_acceptorSocket.DualMode = OptionDualMode;
            }

            // Bind the acceptor socket to the IP endpoint
            m_acceptorSocket.Bind( Endpoint );
            // Refresh the endpoint property based on the actual endpoint created
            Endpoint = (IPEndPoint)m_acceptorSocket.LocalEndPoint;

            // Call the server starting handler
            OnStarting();

            // Start listen to the acceptor socket with the given accepting backlog size
            m_acceptorSocket.Listen( OptionAcceptorBacklog );

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
            StartAccept( m_acceptorEventArg );

            return true;
        }

        /// <summary>
        /// Stop the server
        /// </summary>
        /// <returns>'true' if the server was successfully stopped, 'false' if the server is already stopped</returns>
        protected virtual Boolean Stop()
        {
            if ( !IsStarted )
            {
                return false;
            }

            DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Started stop {nameof( TcpServer )} which listens on {Endpoint}" );

            // Stop accepting new clients
            IsAccepting = false;

            // Reset acceptor event arg
            m_acceptorEventArg.Completed -= OnAsyncCompleted;

            // Call the server stopping handler
            OnStopping();

            try
            {
                // Close the acceptor socket
                m_acceptorSocket.Close();

                // Dispose the acceptor socket
                m_acceptorSocket.Dispose();

                // Dispose event arguments
                m_acceptorEventArg.Dispose();

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

            DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Successfully stopped {nameof( TcpServer )} which listened on {Endpoint}" );

            return true;
        }

        /// <summary>
        /// Restart the server
        /// </summary>
        /// <returns>'true' if the server was successfully restarted, 'false' if the server failed to restart</returns>
        public virtual Boolean Restart()
        {
            if ( !Stop() )
            {
                return false;
            }

            while ( IsStarted )
            {
                Thread.Yield();
            }

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
            if ( !m_acceptorSocket.AcceptAsync( e ) )
            {
                ProcessAccept( e );
            }
        }

        /// <summary>
        /// Process accepted client connection
        /// </summary>
        private void ProcessAccept( SocketAsyncEventArgs e )
        {
            if ( e.SocketError == SocketError.Success )
            {
                DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Successfully accepted socket {e.RemoteEndPoint} by {Endpoint.AddressFamily} {nameof( TcpServer )}" );

                if ( MAX_SESSIONS_COUNT <= m_sessions.Count + 1 )
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
            {
                DsLoggerSet.DefaultLogger.LogFatal( message: $"Accept socket by {Endpoint.AddressFamily} {nameof( TcpServer )} has error: {e.SocketError}" );

                SendError( e.SocketError );
            }

            // Accept the next client connection
            if ( IsAccepting )
            {
                StartAccept( e );
            }
        }

        public async Task<TcpMessageEventArgs> ReceiveAsync( TimeSpan timeoutToRead, TcpSession clientToReadMessage )
        {
            Byte[] readBytes;

            if ( !( clientToReadMessage.Socket.RemoteEndPoint is IPEndPoint ipEndPoint ) )
            {
                throw new InvalidOperationException( message: $"Received message not from {nameof( IPEndPoint )}" );
            }
            else
            {
                var cancelSource = new CancellationTokenSource();

                //because any next code can receive SocketException if it will use clientToReadMessage.Socket.RemoteEndPoint
                var clonedIpEndPoint = new IPEndPoint( ipEndPoint.Address, ipEndPoint.Port );
                var receiveDone = new AsyncAutoResetEvent( set: false );

                try
                {
                    Task<Byte[]> taskReadBytes = clientToReadMessage.Socket.ReadMessageBytesAsync(
                        receiveDone,
                        DsConstants.MAX_CHUNK_READ_PER_ONE_TIME,
                        DsConstants.MAX_AVAILABLE_READ_BYTES,
                        cancelSource.Token
                    );

                    Boolean isReceivedInTime = await receiveDone.WaitAsync( timeoutToRead ).ConfigureAwait( continueOnCapturedContext: false );

                    cancelSource.Cancel();

                    if ( isReceivedInTime )
                    {
                        readBytes = await taskReadBytes.ConfigureAwait(false);
                    }
                    else
                    {
                        //we need to delete it because we will have malformed all next messages
                        UnregisterSession( clientToReadMessage.Id );

                        var timeoutEx = new TimeoutException( $"Timeout to read data from {clonedIpEndPoint}" );
                        DsLoggerSet.DefaultLogger.LogCriticalError( timeoutEx );

                        throw timeoutEx;
                    }
                }
                catch ( SocketException )
                {
                    if ( IsSocketDisposed )
                    {
                        throw new ObjectDisposedException( $"Socket is disposed" );
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

                var receiveResult = new TcpMessageEventArgs
                {
                    Buffer = readBytes,
                    RemoteEndPoint = clonedIpEndPoint,
                    AcceptedSocket = clientToReadMessage.Socket,
                    LocalEndPoint = Endpoint,
                    UnregisterSocket = () => UnregisterSession( clientToReadMessage.Id )
                };

                return receiveResult;
            }
        }

        public async Task<TcpSession> SessionWithMessageAsync()
        {
            TcpSession sessionWithData = null;
            Boolean foundSessionWithData = sessionWithData != null;

            while ( !foundSessionWithData && IsStarted )
            {
                Parallel.ForEach( m_sessions, s_parallelOptions, ( session, loopState ) =>
                 {
                     try
                     {
                         if ( ( session.Value.Socket != null ) && ( session.Value.Socket.Available > 0 ) && session.Value.CanBeUsedByAnotherThread.IsSet )
                         {
                             Interlocked.Exchange( ref sessionWithData, session.Value );
                             loopState.Break();
                         }
                     }
                     catch ( ObjectDisposedException )
                     {
                         ;//do nothing
                     }
                     catch ( SocketException )
                     {
                         ;//do nothing
                     }
                 } );

                foundSessionWithData = sessionWithData != null;

                if ( foundSessionWithData )
                {
                    //only 1 thread should take socket, so when stream got it, another threads will check other sockets for available data
                    foundSessionWithData = await sessionWithData.CanBeUsedByAnotherThread.WaitAsync( m_waitTakeSocket ).ConfigureAwait( continueOnCapturedContext: false );

                    if ( foundSessionWithData )
                    {
                        Int32 availableData = 0;

                        try
                        {
                            availableData = sessionWithData.Socket.Available;
                            foundSessionWithData = availableData > 0;
                        }
                        catch ( ObjectDisposedException )
                        {
                            foundSessionWithData = false;
                        }
                        catch ( SocketException )
                        {
                            foundSessionWithData = false;
                        }

                        if ( foundSessionWithData )
                        {
                            DsLoggerSet.DefaultLogger.LogInfo( $"Found {nameof( TcpSession )} with {availableData} available data to read" );
                        }
                    }
                }

                if ( !foundSessionWithData )
                {
                    await Task.Delay( m_waitForCheckingWaitingSocket ).ConfigureAwait( false );
                    sessionWithData = null;
                }
            }

            if ( sessionWithData != null )
            {
                return sessionWithData;
            }
            else
            {
                throw new ObjectDisposedException( nameof( TcpServer ) );
            }
        }

        /// <summary>
        /// This method is the callback method associated with Socket.AcceptAsync()
        /// operations and is invoked when an accept operation is complete
        /// </summary>
        private void OnAsyncCompleted( Object sender, SocketAsyncEventArgs e )
        {
            if ( IsSocketDisposed )
            {
                DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Accepted socket, but it was disposed. Method {nameof( OnAsyncCompleted )}" );
                return;
            }

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
        protected readonly ConcurrentDictionary<Guid, TcpSession> m_sessions = new ConcurrentDictionary<Guid, TcpSession>();

        /// <summary>
        /// Disconnect all connected sessions
        /// </summary>
        /// <returns>'true' if all sessions were successfully disconnected, 'false' if the server is not started</returns>
        public virtual Boolean DisconnectAll()
        {
            if ( !IsStarted )
            {
                return false;
            }

            // Disconnect all sessions
            foreach ( TcpSession session in m_sessions.Values )
            {
                session.Disconnect( executeDispose: true );
            }

            return true;
        }

        /// <summary>
        /// Find a session with a given Id
        /// </summary>
        /// <param name="id">Session Id</param>
        /// <returns>Session with a given Id or null if the session it not connected</returns>
        public TcpSession FindSession( Guid id ) =>
            // Try to find the required session
            m_sessions.TryGetValue( id, out TcpSession result ) ? result : null;

        /// <summary>
        /// Register a new session
        /// </summary>
        /// <param name="session">Session to register</param>
        internal void RegisterSession( TcpSession session ) =>
            // Register a new session
            m_sessions.TryAdd( session.Id, session );

        /// <summary>
        /// Unregister the oldest session
        /// </summary>
        internal void UnregisterSession()
        {
            TcpSession oldestSession = m_sessions.FirstOrDefault( c => ( c.Value.Socket == null ) || !c.Value.Socket.Connected ).Value;

            if ( oldestSession == null )
            {
                oldestSession = m_sessions.FirstOrDefault( c => c.Value.Socket?.Available == 0 ).Value;

                if ( oldestSession == null )
                {
                    oldestSession = m_sessions.FirstOrDefault().Value;
                }
            }

            if ( m_sessions.Count != 0 )
            {
                UnregisterSession( oldestSession.Id );
            }
        }

        /// <summary>
        /// Unregister session by Id
        /// </summary>
        /// <param name="id">Session Id</param>
        internal void UnregisterSession( Guid id, Boolean executeDispose = true )
        {
            Boolean isFound = m_sessions.TryGetValue( id, out TcpSession tcpSession );
            if ( isFound )
            {
                if ( executeDispose )
                {
                    tcpSession.Dispose();
                }

                // Unregister session by Id
                m_sessions.TryRemove( id, value: out _ );
            }
        }


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
            {
                return false;
            }

            if ( size == 0 )
            {
                return true;
            }

            // Multicast data to all sessions
            foreach ( TcpSession session in m_sessions.Values )
            {
                session.SendAsync( buffer, offset, size );
            }

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
        protected virtual void OnStarting()
        {

        }
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
        protected virtual void OnError( SocketError error )
        {
            //DsLoggerSet.DefaultLogger.LogFatal( message: $"Error in {nameof( TcpServer )} with {nameof( Endpoint )} {Endpoint}: {error}" );
        }

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
            {
                return;
            }

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

        #endregion
    }
}