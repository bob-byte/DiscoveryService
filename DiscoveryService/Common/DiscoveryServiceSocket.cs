using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages;
using LUC.Interfaces;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Common
{
    /// <summary>
    /// New socket methods are marked <a href="Ds"/> in the front of the name, so only there <see cref="m_state"/> will be changed, 
    /// except <see cref="Dispose"/> (there also will be changed) in order to you can easily use this object in <a href="using"/> statement
    /// </summary>
    public class DiscoveryServiceSocket : Socket
    {
        private readonly TimeSpan m_howOftenCheckAcceptedClient;
        private readonly ConcurrentQueue<Socket> m_acceptedSockets;

        private volatile SocketState m_state;

        [Import( typeof( ILoggingService ) )]
        internal static ILoggingService Log { get; private set; }

        /// <inheritdoc/>
        public DiscoveryServiceSocket( AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, BigInteger contactId, ILoggingService loggingService )
            : this( addressFamily, socketType, protocolType, loggingService )
        {
            ContactId = contactId;
        }

        public DiscoveryServiceSocket( AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, ILoggingService loggingService )
            : base( addressFamily, socketType, protocolType )
        {
            m_state = SocketState.Creating;
            Log = loggingService;

            m_howOftenCheckAcceptedClient = TimeSpan.FromSeconds( value: 0.5 );
            m_acceptedSockets = new ConcurrentQueue<Socket>();

            m_state = SocketState.Created;
        }

        public BigInteger ContactId { get; set; }

        public SocketState State => m_state;

        public void DsAccept( TimeSpan timeout, out Socket acceptedSocket )
        {
            VerifyWorkState();

            AutoResetEvent acceptDone = new AutoResetEvent( initialState: false );
            StateObjectForAccept stateAccept = new StateObjectForAccept( this, acceptDone );
            m_state = SocketState.Accepting;

            BeginAccept( new AsyncCallback( AcceptCallback ), stateAccept );
            Boolean isAccepted = acceptDone.WaitOne( timeout );

            if ( isAccepted )
            {
                acceptedSocket = stateAccept.AcceptedSocket;
            }
            else
            {
                throw new TimeoutException();
            }

            stateAccept.Dispose();
        }

        private void AcceptCallback( IAsyncResult asyncResult )
        {
            //Get the socket that handles the client request
            StateObjectForAccept stateAccept = (StateObjectForAccept)asyncResult.AsyncState;
            stateAccept.AcceptedSocket = stateAccept.Listener.EndAccept( asyncResult );
            m_state = SocketState.Accepted;

            //another thread can receive timeout and it will close stateAccept.AcceptDone
            if ( !stateAccept.AcceptDone.SafeWaitHandle.IsClosed )
            {
                //Signal that the connection has been made
                stateAccept.AcceptDone.Set();
            }
        }

        private async Task<Socket> SocketWithNewDataAsync( Int32 lengthStorageOfAcceptedSockets, TimeSpan howOftenCheckAcceptedClient )
        {
            AutoResetEvent firstAcceptDone = new AutoResetEvent( initialState: false );

            Task.Run( async () =>
             {
                 await AcceptNewSockets( lengthStorageOfAcceptedSockets, firstAcceptDone )
                     .ConfigureAwait( continueOnCapturedContext: false );
             } ).ConfigureAwait( false ).GetAwaiter();

            Socket acceptedSocket = null;

            if ( m_acceptedSockets.Count == 0 )
            {
                //wait indefinitely
                firstAcceptDone.WaitOne();
                firstAcceptDone.Close();
            }

            while ( acceptedSocket == null )
            {
                foreach ( Socket socket in m_acceptedSockets )
                {
                    if ( socket.Available > 0 )
                    {
                        acceptedSocket = socket;
                        break;
                    }
                }

                if ( acceptedSocket == null )
                {
                    await Task.Delay( howOftenCheckAcceptedClient ).ConfigureAwait( false );
                }
            }

            return acceptedSocket;
        }

        private async Task AcceptNewSockets( Int32 lengthStorageOfAcceptedSockets, EventWaitHandle acceptDone )
        {
            if ( m_acceptedSockets.Count >= lengthStorageOfAcceptedSockets )
            {
                m_acceptedSockets.TryDequeue( out _ );
            }

            m_state = SocketState.Accepting;
            //AcceptAsync() is extension method, so we can't use it without "this."
            Socket newSocket = await this.AcceptAsync().ConfigureAwait( continueOnCapturedContext: false );
            m_state = SocketState.Accepted;

            m_acceptedSockets.Enqueue( newSocket );

            //another thread can receive timeout and it will close acceptDone
            if ( ( m_acceptedSockets.Count == 1 ) && ( !acceptDone.SafeWaitHandle.IsClosed ) )
            {
                acceptDone.Set();
            }
        }

        public async Task<TcpMessageEventArgs> DsReceiveAsync( TimeSpan timeoutToRead, Int32 lengthStorageOfAcceptedSockets )
        {
            IPEndPoint ipEndPoint;
            Socket clientToReadMessage;
            Byte[] readBytes;

            try
            {
                clientToReadMessage = await SocketWithNewDataAsync( lengthStorageOfAcceptedSockets, m_howOftenCheckAcceptedClient ).
                    ConfigureAwait( continueOnCapturedContext: false );

                AutoResetEvent receiveDone = new AutoResetEvent( initialState: false );

                Task<Byte[]> taskReadBytes = ReadBytesAsync( clientToReadMessage, receiveDone );

                //just configure context without waiting
                taskReadBytes.ConfigureAwait( false ).GetAwaiter();

                //wait until timeoutToRead
                Boolean isReceivedInTime = receiveDone.WaitOne( timeoutToRead );
                if ( isReceivedInTime )
                {
                    readBytes = await taskReadBytes;
                    ipEndPoint = clientToReadMessage.RemoteEndPoint as IPEndPoint;
                }
                else
                {
                    throw new TimeoutException( $"Timeout to read data from {clientToReadMessage.RemoteEndPoint}" );
                }
            }
            catch ( ObjectDisposedException )
            {
                throw;
            }
            catch ( SocketException )
            {
                if ( m_state >= SocketState.Closing )
                {
                    throw new ObjectDisposedException( $"Socket {LocalEndPoint} is disposed" );
                }
                else
                {
                    throw;
                }
            }

            TcpMessageEventArgs receiveResult = new TcpMessageEventArgs();
            if ( ipEndPoint != null )
            {
                receiveResult.Buffer = readBytes;
                receiveResult.RemoteEndPoint = ipEndPoint;
                receiveResult.AcceptedSocket = clientToReadMessage;
                receiveResult.LocalContactId = ContactId;
                receiveResult.LocalEndPoint = LocalEndPoint;
            }
            else
            {
                throw new InvalidOperationException( $"Cannot convert remote end point to {nameof( IPEndPoint )}" );
            }

            return receiveResult;
        }

        /// <summary>
        ///   Reads all available data
        /// </summary>
        private async Task<Byte[]> ReadBytesAsync( Socket socketToRead, EventWaitHandle receiveDone )
        {
            List<Byte> allMessage = new List<Byte>();
            Int32 availableDataToRead = socketToRead.Available;
            m_state = SocketState.Reading;

            for ( Int32 countReadBytes = 1; ( countReadBytes > 0 ) && ( availableDataToRead > 0 ); availableDataToRead = socketToRead.Available )
            {
                ArraySegment<Byte> buffer = new ArraySegment<Byte>( new Byte[ availableDataToRead ] );
                countReadBytes = await socketToRead.ReceiveAsync( buffer, SocketFlags.None ).
                    ConfigureAwait( continueOnCapturedContext: false );
                allMessage.AddRange( buffer );
            }

            m_state = SocketState.AlreadyRead;

            //another thread can receive timeout and it will close receiveDone
            if ( !receiveDone.SafeWaitHandle.IsClosed )
            {
                //Signal that the connection has been made
                receiveDone.Set();
            }

            return allMessage.ToArray();
        }

        public async Task DsConnectAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior )
        {
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await DsConnectAsync( remoteEndPoint, timeoutToConnect ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                DsConnect( remoteEndPoint, timeoutToConnect );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }
        }

        public async Task DsConnectAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect ) =>
            await Task.Run( () => DsConnect( remoteEndPoint, timeoutToConnect ) ).ConfigureAwait( continueOnCapturedContext: false );

        public void DsConnect( EndPoint remoteEndPoint, TimeSpan timeout )
        {
            VerifyWorkState();

            m_state = SocketState.Connecting;

            AutoResetEvent connectDone = new AutoResetEvent( initialState: false );
            BeginConnect( remoteEndPoint, ( asyncResult ) => ConnectCallback( asyncResult, connectDone ), state: this );

            Boolean isConnected = connectDone.WaitOne( timeout );
            connectDone.Close();
            if ( !isConnected )
            {
                throw new TimeoutException();
            }
        }

        protected void VerifyWorkState()
        {
            if ( ( SocketState.Failed <= m_state ) && ( m_state <= SocketState.Closed ) )
            {
                //Wanted to use idle socket
                throw new SocketException( (Int32)SocketError.SocketError );
            }
        }

        private void ConnectCallback( IAsyncResult asyncResult, EventWaitHandle connectDone )
        {
            //Retrieve the socket from the state object
            try
            {
                Socket client = (Socket)asyncResult.AsyncState;

                //Complete the connection
                client.EndConnect( asyncResult );
                m_state = SocketState.Connected;
                Log.LogInfo( $"Socket connected to {client.RemoteEndPoint}" );

                //another thread can receive timeout and it will close connectDone
                if ( !connectDone.SafeWaitHandle.IsClosed )
                {
                    //Signal that the connection has been made
                    connectDone.Set();
                }
            }
            catch ( SocketException ex )
            {
                Log.LogError( ex.ToString() );
            }
            catch ( InvalidCastException ex )
            {
                Log.LogError( ex.ToString() );
            }
        }

        public async Task<Byte[]> DsReceiveAsync( IOBehavior ioBehavior, TimeSpan timeout )
        {
            Byte[] bytesOfResponse;
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                bytesOfResponse = await DsReceiveAsync( timeout ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                bytesOfResponse = DsReceive( timeout );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }

            return bytesOfResponse;
        }

        //TODO: optimize it
        public async Task<Byte[]> DsReceiveAsync( TimeSpan timeout ) =>
            await Task.Run( () => DsReceive( timeout ) ).ConfigureAwait( continueOnCapturedContext: false );

        public Byte[] DsReceive( TimeSpan timeout )
        {
            Boolean isReceived;
            Task<Byte[]> taskReadBytes;

            try
            {
                AutoResetEvent receiveDone = new AutoResetEvent( initialState: false );

                taskReadBytes = ReadBytesAsync( socketToRead: this, receiveDone );
                taskReadBytes.ConfigureAwait( continueOnCapturedContext: false );

                isReceived = receiveDone.WaitOne( timeout );

                receiveDone.Close();
            }
            catch ( SocketException )
            {
                m_state = SocketState.Failed;

                throw;
            }

            if ( isReceived )
            {
                return taskReadBytes.GetAwaiter().GetResult();
            }
            else
            {
                throw new TimeoutException();
            }
        }

        public async Task DsSendAsync( Byte[] bytesToSend, TimeSpan timeoutToSend, IOBehavior ioBehavior )
        {
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await DsSendAsync( bytesToSend, timeoutToSend ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                DsSend( bytesToSend, timeoutToSend );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }
        }

        //TODO: optimize it
        public async Task DsSendAsync( Byte[] bytesToSend, TimeSpan timeout ) =>
            await Task.Run( () => DsSend( bytesToSend, timeout ) ).ConfigureAwait( continueOnCapturedContext: false );

        public void DsSend( Byte[] bytesToSend, TimeSpan timeout )
        {
            VerifyConnected();

            AutoResetEvent sendDone = new AutoResetEvent( initialState: false );

            m_state = SocketState.SendingBytes;

            //Begin sending the data to the remote device
            BeginSend(
                bytesToSend,
                offset: 0,
                bytesToSend.Length,
                SocketFlags.None,
                ( asyncResult ) => SendCallback( asyncResult, sendDone ),
                state: this
            );

            Boolean isSent = sendDone.WaitOne( timeout );
            sendDone.Close();
            if ( !isSent )
            {
                throw new TimeoutException();
            }
        }

        //It throws exceptions, not <see cref="Boolean"/> value, because if socket isn't connected we should immediately end method
        public void VerifyConnected()
        {
            if ( m_state == SocketState.Closed )
            {
                throw new ObjectDisposedException( nameof( DiscoveryServiceSocket ) );
            }
            else if ( !Connected || ( ( SocketState.Disconnected <= m_state ) && ( m_state <= SocketState.Closed ) ) )
            {
                throw new SocketException( (Int32)SocketError.NotConnected );
            }
        }

        private void SendCallback( IAsyncResult asyncResult, EventWaitHandle sendDone )
        {
            //Retrieve the socket from the state object
            Socket client = (Socket)asyncResult.AsyncState;

            //Complete sending the data to the remote device
            Int32 bytesSent = client.EndSend( asyncResult );

            m_state = SocketState.SentBytes;
            Log.LogInfo( $"Sent {bytesSent} bytes to {client.RemoteEndPoint}" );

            //another thread can receive timoeut and it will close sendDone
            if ( !sendDone.SafeWaitHandle.IsClosed )
            {
                //Signal that the connection has been made
                sendDone.Set();
            }
        }

        public async Task DsDisconnectAsync( IOBehavior ioBehavior, Boolean reuseSocket, TimeSpan timeout )
        {
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await DsDisconnectAsync( reuseSocket, timeout ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                DsDisconnect( reuseSocket, timeout );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} = {default( IOBehavior )}" );
            }
        }

        public void DsDisconnect( Boolean reuseSocket, TimeSpan timeout )
        {
            VerifyConnected();

            AutoResetEvent disconnectDone = new AutoResetEvent( initialState: false );

            m_state = SocketState.Disconnecting;
            BeginDisconnect(
                reuseSocket,
                ( asyncResult ) => DisconnectCallback( asyncResult, disconnectDone ),
                state: this
            );

            Boolean isDisconnected = disconnectDone.WaitOne( timeout );
            disconnectDone.Close();
            if ( !isDisconnected )
            {
                throw new TimeoutException();
            }
        }

        private void DisconnectCallback( IAsyncResult asyncResult, EventWaitHandle disconnectDone )
        {
            Socket socket = (Socket)asyncResult.AsyncState;
            socket.EndDisconnect( asyncResult );

            m_state = SocketState.Disconnected;

            //another thread can receive timoeut and it will close disconnectDone
            if ( !disconnectDone.SafeWaitHandle.IsClosed )
            {
                //Signal that the connection has been made
                disconnectDone.Set();
            }
        }

        public async Task DsDisconnectAsync( Boolean reuseSocket, TimeSpan timeout ) =>
            await Task.Run( () => DsDisconnect( reuseSocket, timeout ) ).ConfigureAwait( continueOnCapturedContext: false );

        private void VerifyState( SocketState state )
        {
            if ( m_state != state )
            {
                Log.LogError( $"Session {RemoteEndPoint} should have SessionStateExpected {state} but was SessionState {m_state}" );
                throw new InvalidOperationException( $"Expected state to be {state} but was {m_state}." );
            }
        }

        public new void Dispose()
        {
            if ( ( SocketState.Created <= m_state ) && ( m_state <= SocketState.Disconnected ) )
            {
                m_state = SocketState.Closing;

                foreach ( Socket acceptedSocket in m_acceptedSockets )
                {
                    try
                    {
                        acceptedSocket.Dispose();
                    }
                    catch
                    {
                        //eat it
                    }
                }

                base.Dispose();

                m_state = SocketState.Closed;
            }
            else
            {
                throw new ObjectDisposedException( "Try to dispose already closed socket" );
            }
        }
    }
}
