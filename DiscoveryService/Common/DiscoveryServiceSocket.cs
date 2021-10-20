using LUC.DiscoveryService.Common.Extensions;
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
        private ConcurrentQueue<Socket> m_acceptedSockets;

        protected volatile SocketState m_state;

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

            m_state = SocketState.Created;
        }

        public BigInteger ContactId { get; set; }

        public SocketState State { get => m_state; protected set => m_state = value; }

        public void DsAccept( TimeSpan timeout, out Socket acceptedSocket )
        {
            VerifyWorkState();

            AutoResetEvent acceptDone = new AutoResetEvent( initialState: false );
            StateObjectForAccept stateAccept = new StateObjectForAccept( this, acceptDone );
            m_state = SocketState.Accepting;

            try
            {
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
            }
            finally
            {
                stateAccept.Dispose();
            }            
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
            if(m_acceptedSockets == null)
            {
                lock(m_acceptedSockets)
                {
                    if(m_acceptedSockets == null)
                    {
                        m_acceptedSockets = new ConcurrentQueue<Socket>();
                    }
                }
            }

            if ( m_acceptedSockets.Count >= lengthStorageOfAcceptedSockets )
            {
                m_acceptedSockets.TryDequeue( out _ );
            }

            m_state = SocketState.Accepting;
            //AcceptAsync() is extension method, so we can't use it without "this."
            Socket newSocket = await this.AcceptAsync();
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

                Task<(Byte[], SocketException)> taskReadBytes = ReadBytesAsync( clientToReadMessage, receiveDone );

                //just configure context without waiting
                taskReadBytes.ConfigureAwait( false ).GetAwaiter();

                //wait until timeoutToRead
                Boolean isReceivedInTime = receiveDone.WaitOne( timeoutToRead );

                SocketException socketException;
                (readBytes, socketException) = await taskReadBytes;
                HandleSocketOperationResult( isReceivedInTime, socketException );

                ipEndPoint = clientToReadMessage.RemoteEndPoint as IPEndPoint;
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
        private async Task<(Byte[] readBytes, SocketException socketException)> ReadBytesAsync( Socket socketToRead, EventWaitHandle receiveDone )
        {
            m_state = SocketState.Reading;
            SocketException socketException = new SocketException( (Int32)SocketError.Success );
            Byte[] readBytes = new Byte[ 0 ];
            try
            {
                readBytes = await socketToRead.ReadAllAvailableBytesAsync( receiveDone, Constants.MAX_CHUNK_SIZE, Constants.MaxAvailableReadBytes ).
                     ConfigureAwait( continueOnCapturedContext: false );
            }
            catch ( SocketException ex )
            {
                //in case if ThreadAbortException is thrown (the whole finally block will be finished first, as it is a critical section). 
                try
                {
                    ;//do nothing
                }
                finally
                {
                    m_state = SocketState.Failed;
                    socketException = ex;
                    receiveDone.SafeSet( isSet: out _ );
                }

                return (readBytes, socketException);
            }

            m_state = SocketState.AlreadyRead;

            return (readBytes, socketException);
        }

        public async Task DsConnectAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, CancellationToken cancellationToken = default )
        {
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await DsConnectAsync( remoteEndPoint, timeoutToConnect, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                DsConnect( remoteEndPoint, timeoutToConnect, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }
        }

        public async Task DsConnectAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsConnect( remoteEndPoint, timeoutToConnect, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

        public void DsConnect( EndPoint remoteEndPoint, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            SocketOperation( SocketAsyncOperation.Connect, ( asyncCallback ) => BeginConnect( remoteEndPoint, asyncCallback, state: this ), timeout, cancellationToken );
        }

        protected void VerifyWorkState()
        {
            if ( ( SocketState.Failed <= m_state ) && ( m_state <= SocketState.Closed ) )
            {
                //Wanted to use idle socket
                throw new SocketException( (Int32)SocketError.SocketError );
            }
        }

        public async Task<Byte[]> DsReceiveAsync( IOBehavior ioBehavior, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            Byte[] bytesOfResponse;
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                bytesOfResponse = await DsReceiveAsync( timeout, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                bytesOfResponse = DsReceive( timeout, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }

            return bytesOfResponse;
        }

        //TODO: optimize it
        public async Task<Byte[]> DsReceiveAsync( TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsReceive( timeout, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

        public Byte[] DsReceive( TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            Boolean isTaskEnded;
            Task<(Byte[], SocketException)> taskReadBytes;
            AutoResetEvent taskDone = new AutoResetEvent( initialState: false );

            try
            {
                taskReadBytes = ReadBytesAsync( socketToRead: this, taskDone );
                taskReadBytes.ConfigureAwait( continueOnCapturedContext: false );

                isTaskEnded = IsInTimeCompleted( taskDone, timeout, cancellationToken );
            }
            finally
            {
                taskDone.Close();
            }

            (Byte[] readBytes, SocketException socketException) = taskReadBytes.GetAwaiter().GetResult();
            HandleSocketOperationResult( isTaskEnded, socketException );
            return readBytes;
        }
        
        private Boolean IsInTimeCompleted(EventWaitHandle eventWait, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Boolean isInTimeCompleted = cancellationToken != default ?
                cancellationToken.WaitHandle.WaitOne( timeout ) :
                eventWait.WaitOne( timeout );
            return isInTimeCompleted;
        }

        private void HandleSocketOperationResult(Boolean isInTime, SocketException socketException)
        {
            if ( socketException.SocketErrorCode != SocketError.Success )
            {
                throw socketException;
            }
            else if ( !isInTime )
            {
                throw new TimeoutException();
            }
        }

        public async Task DsSendAsync( Byte[] bytesToSend, TimeSpan timeoutToSend, IOBehavior ioBehavior, CancellationToken cancellationToken = default )
        {
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await DsSendAsync( bytesToSend, timeoutToSend, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                DsSend( bytesToSend, timeoutToSend, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }
        }

        //TODO: optimize it
        public async Task DsSendAsync( Byte[] bytesToSend, TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsSend( bytesToSend, timeout, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

        public void DsSend( Byte[] bytesToSend, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            SocketOperation( SocketAsyncOperation.Send, ( asyncCallback ) => 
                BeginSend(
                    bytesToSend,
                    offset: 0,
                    bytesToSend.Length,
                    SocketFlags.None,
                    asyncCallback,
                    state: this
                ), timeout, cancellationToken );

            Log.LogInfo( $"Sent {bytesToSend.Length} bytes" );
        }

        //It throws exceptions, not <see cref="Boolean"/> value, because if socket isn't connected we should immediately end method where VerifyConnected is call
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

        public async Task DsDisconnectAsync( IOBehavior ioBehavior, Boolean reuseSocket, 
            TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await DsDisconnectAsync( reuseSocket, timeout, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                DsDisconnect( reuseSocket, timeout, cancellationToken );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} = {default( IOBehavior )}" );
            }
        }

        public void DsDisconnect( Boolean reuseSocket, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            SocketOperation(SocketAsyncOperation.Disconnect, (asyncCallback) => BeginDisconnect(reuseSocket, asyncCallback, state: this), timeout, cancellationToken);
        }

        public async Task DsDisconnectAsync( Boolean reuseSocket, TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            await Task.Run( () => DsDisconnect( reuseSocket, timeout, cancellationToken ) ).ConfigureAwait( continueOnCapturedContext: false );

        public void SocketOperation(SocketAsyncOperation socketOp, Func<AsyncCallback, IAsyncResult> beginOperation, TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            switch ( socketOp )
            {
                case SocketAsyncOperation.Connect:
                {
                    VerifyWorkState();
                    State = SocketState.Connecting;

                    break;
                }

                case SocketAsyncOperation.Send:
                {
                    VerifyConnected();
                    State = SocketState.SendingBytes;

                    break;
                }

                case SocketAsyncOperation.Disconnect:
                {
                    VerifyConnected();
                    State = SocketState.Disconnecting;

                    break;
                }

                default:
                {
                    throw new NotImplementedException( "Doesn't supported" );
                }
            }

            AutoResetEvent operationDone = new AutoResetEvent( initialState: false );
            SocketException socketException = new SocketException((Int32)SocketError.Success);
            AsyncCallback asyncCallback = ( asyncResult ) => SocketOperationCallback( socketOp, asyncResult, operationDone, out socketException );

            try
            {
                beginOperation( asyncCallback );
                WaitResultOfAsyncSocketOperation( operationDone, timeout, ref socketException, cancellationToken );
            }
            finally
            {
                operationDone.Close();
            }
        }

        /// <param name="socketException">
        /// If it is not ref parameter, it will not be updated by another thread
        /// </param>
        private void WaitResultOfAsyncSocketOperation( EventWaitHandle operationDone, TimeSpan timeout, ref SocketException socketException, CancellationToken cancellationToken = default)
        {
            Boolean isInTime = cancellationToken != default ? 
                cancellationToken.WaitHandle.WaitOne( timeout ) : 
                operationDone.WaitOne( timeout );

            Exception exception;

            if ( ( socketException.SocketErrorCode == SocketError.Success ) && ( isInTime ) )
            {
                return;
            }
            else if ( socketException.SocketErrorCode != SocketError.Success )
            {
                exception = socketException;
            }
            else if ( !isInTime )
            {
                exception = new TimeoutException();
            }
            else
            {
                //it can't be
                exception = new InvalidProgramException();
            }

            throw exception;
        }

        //Now it is only for Connect, Send and Disconnect 
        private void SocketOperationCallback(SocketAsyncOperation socketOp, IAsyncResult asyncResult, EventWaitHandle operationDone, out SocketException socketException)
        {
            Socket socket = (Socket)asyncResult.AsyncState;
            socketException = new SocketException((Int32)SocketError.Success);

            try
            {
                switch ( socketOp )
                {
                    case SocketAsyncOperation.Connect:
                    {
                        socket.EndConnect( asyncResult );
                        State = SocketState.Connected;

                        break;
                    }

                    case SocketAsyncOperation.Send:
                    {
                        socket.EndSend( asyncResult );
                        State = SocketState.SentBytes;

                        break;
                    }

                    case SocketAsyncOperation.Disconnect:
                    {
                        socket.EndDisconnect( asyncResult );
                        State = SocketState.Disconnected;

                        break;
                    }

                    default:
                    {
                        throw new NotImplementedException( "Doesn't supported" );
                    }
                }

                operationDone.SafeSet( out _ );
            }
            catch (SocketException ex)
            {
                //in case if ThreadAbortException is thrown (the whole finally block will be finished first, as it is a critical section). 
                try
                {
                    ;//do nothing
                }
                finally
                {
                    socketException = ex;
                    State = SocketState.Failed;

                    operationDone.SafeSet( isSet: out _ );
                }
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
