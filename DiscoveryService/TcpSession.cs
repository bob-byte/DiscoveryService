using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;

namespace LUC.DiscoveryService
{
    /// <summary>
    /// TCP session is used to read and write data from the connected TCP client
    /// </summary>
    /// <remarks>Thread-safe</remarks>
    class TcpSession : IDisposable
    {
        /// <summary>
        /// Initialize the session with a given server
        /// </summary>
        /// <param name="server">TCP server</param>
        public TcpSession( TcpServer server )
        {
            Id = Guid.NewGuid();
            Server = server;
            OptionReceiveBufferSize = server.OptionReceiveBufferSize;
            OptionSendBufferSize = server.OptionSendBufferSize;
        }

        /// <summary>
        /// Session Id
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Server
        /// </summary>
        public TcpServer Server { get; }
        /// <summary>
        /// Socket
        /// </summary>
        public Socket Socket { get; private set; }

        /// <summary>
        /// Number of bytes pending sent by the session
        /// </summary>
        public Int64 BytesPending { get; private set; }
        /// <summary>
        /// Number of bytes sending by the session
        /// </summary>
        public Int64 BytesSending { get; private set; }
        /// <summary>
        /// Number of bytes sent by the session
        /// </summary>
        public Int64 BytesSent { get; private set; }
        /// <summary>
        /// Number of bytes received by the session
        /// </summary>
        public Int64 BytesReceived { get; private set; }

        /// <summary>
        /// Option: receive buffer limit
        /// </summary>
        public Int32 OptionReceiveBufferLimit { get; set; } = 0;
        /// <summary>
        /// Option: receive buffer size
        /// </summary>
        public Int32 OptionReceiveBufferSize { get; set; } = 8192;
        /// <summary>
        /// Option: send buffer limit
        /// </summary>
        public Int32 OptionSendBufferLimit { get; set; } = 0;
        /// <summary>
        /// Option: send buffer size
        /// </summary>
        public Int32 OptionSendBufferSize { get; set; } = 8192;

        #region Connect/Disconnect session

        /// <summary>
        /// Is the session connected?
        /// </summary>
        public Boolean IsConnected { get; private set; }

        /// <summary>
        /// Connect the session
        /// </summary>
        /// <param name="socket">Session socket</param>
        internal void Connect( Socket socket )
        {
            Socket = socket;

            // Update the session socket disposed flag
            IsSocketDisposed = false;

            // Setup buffers
            _receiveBuffer = new CodingData.Buffer();
            _sendBufferMain = new CodingData.Buffer();
            _sendBufferFlush = new CodingData.Buffer();

            // Setup event args
            _receiveEventArg = new SocketAsyncEventArgs();
            _receiveEventArg.Completed += OnAsyncCompleted;
            _sendEventArg = new SocketAsyncEventArgs();
            _sendEventArg.Completed += OnAsyncCompleted;

            // Apply the option: keep alive
            if ( Server.OptionKeepAlive )
                Socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true );
            // Apply the option: no delay
            if ( Server.OptionNoDelay )
                Socket.SetSocketOption( SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true );

            // Prepare receive & send buffers
            _receiveBuffer.Reserve( OptionReceiveBufferSize );
            _sendBufferMain.Reserve( OptionSendBufferSize );
            _sendBufferFlush.Reserve( OptionSendBufferSize );

            // Reset statistic
            BytesPending = 0;
            BytesSending = 0;
            BytesSent = 0;
            BytesReceived = 0;

            // Call the session connecting handler
            OnConnecting();

            // Call the session connecting handler in the server
            Server.OnConnectingInternal( this );

            // Update the connected flag
            IsConnected = true;

            // Try to receive something from the client
            //TryReceive();

            // Check the socket disposed state: in some rare cases it might be disconnected while receiving!
            if ( IsSocketDisposed )
                return;

            // Call the session connected handler
            OnConnected();

            // Call the session connected handler in the server
            Server.OnConnectedInternal( this );

            // Call the empty send buffer handler
            if ( _sendBufferMain.IsEmpty )
                OnEmpty();
        }

        /// <summary>
        /// Disconnect the session
        /// </summary>
        /// <returns>'true' if the section was successfully disconnected, 'false' if the section is already disconnected</returns>
        public virtual Boolean Disconnect()
        {
            if ( !IsConnected )
                return false;

            // Reset event args
            _receiveEventArg.Completed -= OnAsyncCompleted;
            _sendEventArg.Completed -= OnAsyncCompleted;

            // Call the session disconnecting handler
            OnDisconnecting();

            // Call the session disconnecting handler in the server
            Server.OnDisconnectingInternal( this );

            try
            {
                try
                {
                    // Shutdown the socket associated with the client
                    Socket.Shutdown( SocketShutdown.Both );
                }
                catch ( SocketException ) { }

                // Close the session socket
                Socket.Close();

                // Dispose the session socket
                Socket.Dispose();

                // Dispose event arguments
                _receiveEventArg.Dispose();
                _sendEventArg.Dispose();

                // Update the session socket disposed flag
                IsSocketDisposed = true;
            }
            catch ( ObjectDisposedException ) { }

            // Update the connected flag
            IsConnected = false;

            // Update sending/receiving flags
            _receiving = false;
            _sending = false;

            // Clear send/receive buffers
            ClearBuffers();

            // Call the session disconnected handler
            OnDisconnected();

            // Call the session disconnected handler in the server
            Server.OnDisconnectedInternal( this );

            // Unregister session
            Server.UnregisterSession( Id );

            return true;
        }

        #endregion

        #region Send/Recieve data

        // Receive buffer
        private Boolean _receiving;
        private CodingData.Buffer _receiveBuffer;
        private SocketAsyncEventArgs _receiveEventArg;
        // Send buffer
        private readonly Object _sendLock = new Object();
        private Boolean _sending;
        private CodingData.Buffer _sendBufferMain;
        private CodingData.Buffer _sendBufferFlush;
        private SocketAsyncEventArgs _sendEventArg;
        private Int64 _sendBufferFlushOffset;

        /// <summary>
        /// Send data to the client (synchronous)
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <returns>Size of sent data</returns>
        public virtual Int64 Send( Byte[] buffer ) => Send( buffer, 0, buffer.Length );

        /// <summary>
        /// Send data to the client (synchronous)
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>Size of sent data</returns>
        public virtual Int64 Send( Byte[] buffer, Int64 offset, Int64 size )
        {
            if ( !IsConnected )
                return 0;

            if ( size == 0 )
                return 0;

            // Sent data to the client
            Int64 sent = Socket.Send( buffer, (Int32)offset, (Int32)size, SocketFlags.None, out SocketError ec );
            if ( sent > 0 )
            {
                // Update statistic
                BytesSent += sent;
                Interlocked.Add( ref Server._bytesSent, size );

                // Call the buffer sent handler
                OnSent( sent, BytesPending + BytesSending );
            }

            // Check for socket error
            if ( ec != SocketError.Success )
            {
                SendError( ec );
                Disconnect();
            }

            return sent;
        }

        /// <summary>
        /// Send text to the client (synchronous)
        /// </summary>
        /// <param name="text">Text string to send</param>
        /// <returns>Size of sent data</returns>
        public virtual Int64 Send( String text ) => Send( Encoding.UTF8.GetBytes( text ) );

        /// <summary>
        /// Send data to the client (asynchronous)
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
        public virtual Boolean SendAsync( Byte[] buffer ) => SendAsync( buffer, 0, buffer.Length );

        /// <summary>
        /// Send data to the client (asynchronous)
        /// </summary>
        /// <param name="buffer">Buffer to send</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
        public virtual Boolean SendAsync( Byte[] buffer, Int64 offset, Int64 size )
        {
            if ( !IsConnected )
                return false;

            if ( size == 0 )
                return true;

            lock ( _sendLock )
            {
                // Check the send buffer limit
                if ( ( ( _sendBufferMain.Size + size ) > OptionSendBufferLimit ) && ( OptionSendBufferLimit > 0 ) )
                {
                    SendError( SocketError.NoBufferSpaceAvailable );
                    return false;
                }

                // Fill the main send buffer
                _sendBufferMain.Append( buffer, offset, size );

                // Update statistic
                BytesPending = _sendBufferMain.Size;

                // Avoid multiple send handlers
                if ( _sending )
                    return true;
                else
                    _sending = true;

                // Try to send the main buffer
                TrySend();
            }

            return true;
        }

        /// <summary>
        /// Send text to the client (asynchronous)
        /// </summary>
        /// <param name="text">Text string to send</param>
        /// <returns>'true' if the text was successfully sent, 'false' if the session is not connected</returns>
        public virtual Boolean SendAsync( String text ) => SendAsync( Encoding.UTF8.GetBytes( text ) );

        /// <summary>
        /// Receive data from the client (synchronous)
        /// </summary>
        /// <param name="buffer">Buffer to receive</param>
        /// <returns>Size of received data</returns>
        public virtual Int64 Receive( Byte[] buffer ) => Receive( buffer, 0, buffer.Length );

        /// <summary>
        /// Receive data from the client (synchronous)
        /// </summary>
        /// <param name="buffer">Buffer to receive</param>
        /// <param name="offset">Buffer offset</param>
        /// <param name="size">Buffer size</param>
        /// <returns>Size of received data</returns>
        public virtual Int64 Receive( Byte[] buffer, Int64 offset, Int64 size )
        {
            if ( !IsConnected )
                return 0;

            if ( size == 0 )
                return 0;

            // Receive data from the client
            Int64 received = Socket.Receive( buffer, (Int32)offset, (Int32)size, SocketFlags.None, out SocketError ec );
            if ( received > 0 )
            {
                // Update statistic
                BytesReceived += received;
                Interlocked.Add( ref Server._bytesReceived, received );

                // Call the buffer received handler
                OnReceived( buffer, 0, received );
            }

            // Check for socket error
            if ( ec != SocketError.Success )
            {
                SendError( ec );
                Disconnect();
            }

            return received;
        }

        /// <summary>
        /// Receive text from the client (synchronous)
        /// </summary>
        /// <param name="size">Text size to receive</param>
        /// <returns>Received text</returns>
        public virtual String Receive( Int64 size )
        {
            Byte[] buffer = new Byte[ size ];
            Int64 length = Receive( buffer );
            return Encoding.UTF8.GetString( buffer, 0, (Int32)length );
        }

        /// <summary>
        /// Receive data from the client (asynchronous)
        /// </summary>
        public virtual void ReceiveAsync() =>
            // Try to receive data from the client
            TryReceive();

        /// <summary>
        /// Try to receive new data
        /// </summary>
        private void TryReceive()
        {
            if ( _receiving )
                return;

            if ( !IsConnected )
                return;

            Boolean process = true;

            while ( process )
            {
                process = false;

                try
                {
                    // Async receive with the receive handler
                    _receiving = true;
                    _receiveEventArg.SetBuffer( _receiveBuffer.Data, 0, (Int32)_receiveBuffer.Capacity );
                    if ( !Socket.ReceiveAsync( _receiveEventArg ) )
                        process = ProcessReceive( _receiveEventArg );
                }
                catch ( ObjectDisposedException ) { }
            }
        }

        /// <summary>
        /// Try to send pending data
        /// </summary>
        private void TrySend()
        {
            if ( !IsConnected )
                return;

            Boolean empty = false;
            Boolean process = true;

            while ( process )
            {
                process = false;

                lock ( _sendLock )
                {
                    // Is previous socket send in progress?
                    if ( _sendBufferFlush.IsEmpty )
                    {
                        // Swap flush and main buffers
                        _sendBufferFlush = Interlocked.Exchange( ref _sendBufferMain, _sendBufferFlush );
                        _sendBufferFlushOffset = 0;

                        // Update statistic
                        BytesPending = 0;
                        BytesSending += _sendBufferFlush.Size;

                        // Check if the flush buffer is empty
                        if ( _sendBufferFlush.IsEmpty )
                        {
                            // Need to call empty send buffer handler
                            empty = true;

                            // End sending process
                            _sending = false;
                        }
                    }
                    else
                        return;
                }

                // Call the empty send buffer handler
                if ( empty )
                {
                    OnEmpty();
                    return;
                }

                try
                {
                    // Async write with the write handler
                    _sendEventArg.SetBuffer( _sendBufferFlush.Data, (Int32)_sendBufferFlushOffset, (Int32)( _sendBufferFlush.Size - _sendBufferFlushOffset ) );
                    if ( !Socket.SendAsync( _sendEventArg ) )
                        process = ProcessSend( _sendEventArg );
                }
                catch ( ObjectDisposedException ) { }
            }
        }

        /// <summary>
        /// Clear send/receive buffers
        /// </summary>
        private void ClearBuffers()
        {
            lock ( _sendLock )
            {
                // Clear send buffers
                _sendBufferMain.Clear();
                _sendBufferFlush.Clear();
                _sendBufferFlushOffset = 0;

                // Update statistic
                BytesPending = 0;
                BytesSending = 0;
            }
        }

        #endregion

        #region IO processing

        /// <summary>
        /// This method is called whenever a receive or send operation is completed on a socket
        /// </summary>
        private void OnAsyncCompleted( Object sender, SocketAsyncEventArgs e )
        {
            if ( IsSocketDisposed )
                return;

            // Determine which type of operation just completed and call the associated handler
            switch ( e.LastOperation )
            {
                case SocketAsyncOperation.Receive:
                    if ( ProcessReceive( e ) )
                        TryReceive();
                    break;
                case SocketAsyncOperation.Send:
                    if ( ProcessSend( e ) )
                        TrySend();
                    break;
                default:
                    throw new ArgumentException( "The last operation completed on the socket was not a receive or send" );
            }

        }

        /// <summary>
        /// This method is invoked when an asynchronous receive operation completes
        /// </summary>
        private Boolean ProcessReceive( SocketAsyncEventArgs e )
        {
            if ( !IsConnected )
                return false;

            Int64 size = e.BytesTransferred;

            // Received some data from the client
            if ( size > 0 )
            {
                // Update statistic
                BytesReceived += size;
                Interlocked.Add( ref Server._bytesReceived, size );

                // Call the buffer received handler
                OnReceived( _receiveBuffer.Data, 0, size );

                // If the receive buffer is full increase its size
                if ( _receiveBuffer.Capacity == size )
                {
                    // Check the receive buffer limit
                    if ( ( ( 2 * size ) > OptionReceiveBufferLimit ) && ( OptionReceiveBufferLimit > 0 ) )
                    {
                        SendError( SocketError.NoBufferSpaceAvailable );
                        Disconnect();
                        return false;
                    }

                    _receiveBuffer.Reserve( 2 * size );
                }
            }

            _receiving = false;

            // Try to receive again if the session is valid
            if ( e.SocketError == SocketError.Success )
            {
                // If zero is returned from a read operation, the remote end has closed the connection
                if ( size > 0 )
                    return true;
                else
                    Disconnect();
            }
            else
            {
                SendError( e.SocketError );
                Disconnect();
            }

            return false;
        }

        /// <summary>
        /// This method is invoked when an asynchronous send operation completes
        /// </summary>
        private Boolean ProcessSend( SocketAsyncEventArgs e )
        {
            if ( !IsConnected )
                return false;

            Int64 size = e.BytesTransferred;

            // Send some data to the client
            if ( size > 0 )
            {
                // Update statistic
                BytesSending -= size;
                BytesSent += size;
                Interlocked.Add( ref Server._bytesSent, size );

                // Increase the flush buffer offset
                _sendBufferFlushOffset += size;

                // Successfully send the whole flush buffer
                if ( _sendBufferFlushOffset == _sendBufferFlush.Size )
                {
                    // Clear the flush buffer
                    _sendBufferFlush.Clear();
                    _sendBufferFlushOffset = 0;
                }

                // Call the buffer sent handler
                OnSent( size, BytesPending + BytesSending );
            }

            // Try to send again if the session is valid
            if ( e.SocketError == SocketError.Success )
                return true;
            else
            {
                SendError( e.SocketError );
                Disconnect();
                return false;
            }
        }

        #endregion

        #region Session handlers

        /// <summary>
        /// Handle client connecting notification
        /// </summary>
        protected virtual void OnConnecting() { }
        /// <summary>
        /// Handle client connected notification
        /// </summary>
        protected virtual void OnConnected() { }
        /// <summary>
        /// Handle client disconnecting notification
        /// </summary>
        protected virtual void OnDisconnecting() { }
        /// <summary>
        /// Handle client disconnected notification
        /// </summary>
        protected virtual void OnDisconnected() { }

        /// <summary>
        /// Handle buffer received notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        /// <remarks>
        /// Notification is called when another chunk of buffer was received from the client
        /// </remarks>
        protected virtual void OnReceived( Byte[] buffer, Int64 offset, Int64 size ) { }
        /// <summary>
        /// Handle buffer sent notification
        /// </summary>
        /// <param name="sent">Size of sent buffer</param>
        /// <param name="pending">Size of pending buffer</param>
        /// <remarks>
        /// Notification is called when another chunk of buffer was sent to the client.
        /// This handler could be used to send another buffer to the client for instance when the pending size is zero.
        /// </remarks>
        protected virtual void OnSent( Int64 sent, Int64 pending ) { }

        /// <summary>
        /// Handle empty send buffer notification
        /// </summary>
        /// <remarks>
        /// Notification is called when the send buffer is empty and ready for a new data to send.
        /// This handler could be used to send another buffer to the client.
        /// </remarks>
        protected virtual void OnEmpty() { }

        /// <summary>
        /// Handle error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        protected virtual void OnError( SocketError error ) { }

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
        /// Session socket disposed flag
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
                    Disconnect();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...

                // Mark as disposed.
                IsDisposed = true;
            }
        }

        // Use C# destructor syntax for finalization code.
        ~TcpSession()
        {
            // Simply call Dispose(false).
            Dispose( false );
        }

        #endregion
    }
}
