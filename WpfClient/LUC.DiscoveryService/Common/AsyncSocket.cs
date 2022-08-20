using LUC.DiscoveryServices.Common.Extensions;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;

using Nito.AsyncEx;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Common
{
    /// <summary>
    /// Async socket with cancellable operations.
    /// </summary>
    /// <remarks>
    /// New socket methods are marked <a href="Ds"/> in the front of the name, so <see cref="State"/> property will be changed only there.
    /// </remarks>
    public partial class AsyncSocket : Socket
    {
        public AsyncSocket( 
            AddressFamily addressFamily, 
            SocketType socketType, 
            ProtocolType protocolType 
        ) : base( addressFamily, socketType, protocolType )
        {
            ReceiveTimeout = (Int32)DsConstants.ReceiveOneChunkTimeout.TotalMilliseconds;
            SendTimeout = (Int32)DsConstants.SendTimeout.TotalMilliseconds;
            State = SocketState.Created;
        }

        public SocketState State { get; protected set; }

        public void DsAccept( TimeSpan timeout, out Socket acceptedSocket )
        {
            VerifyWorkState();

            var acceptDone = new AutoResetEvent( initialState: false );
            var stateAccept = new StateObjectForAccept( this, acceptDone );
            State = SocketState.Accepting;

            try
            {
                BeginAccept( new AsyncCallback( AcceptCallback ), stateAccept );
                Boolean isAccepted = acceptDone.WaitOne( timeout );

                if ( isAccepted )
                {
                    State = SocketState.Accepted;
                    acceptedSocket = stateAccept.AcceptedSocket;
                }
                else
                {
                    throw new TimeoutException( message: $"Accept socket timeout occured" );
                }
            }
            finally
            {
                stateAccept.Dispose();
            }
        }

        //public SslStream DsSslConnect( EndPoint remoteEndPoint, TimeSpan timeout, CancellationToken cancellationToken = default )
        //{
        //    var networkStream = new NetworkStream( this );
        //    var sslStream = new SslStream( networkStream );
        //    sslStream.AuthenticateAsClient()
        //}

        public void DsConnect( 
            EndPoint remoteEndPoint, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default 
        ) => SocketOperation(
                 SocketAsyncOperation.Connect,
                 ( asyncCallback ) =>
                 {
                     DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Start to connect with {remoteEndPoint}" );
                     return BeginConnect( remoteEndPoint, asyncCallback, state: this );
                 },
                 timeout, cancellationToken
             );

        public Task DsConnectAsync( 
            EndPoint remoteEndPoint, 
            TimeSpan timeoutToConnect, 
            IoBehavior ioBehavior, 
            CancellationToken cancellationToken = default 
        ){
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                return DsConnectAsync( remoteEndPoint, timeoutToConnect, cancellationToken );
            }
            else if ( ioBehavior == IoBehavior.Synchronous )
            {
                DsConnect( remoteEndPoint, timeoutToConnect, cancellationToken );
                return Task.CompletedTask;
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }
        }

        public Task DsConnectAsync( 
            EndPoint remoteEndPoint, 
            TimeSpan timeoutToConnect, 
            CancellationToken cancellationToken = default 
        ) => Task.Run( () => DsConnect( remoteEndPoint, timeoutToConnect, cancellationToken ) );

        public void DsDisconnect( 
            Boolean reuseSocket, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default 
        ) => SocketOperation(
                 SocketAsyncOperation.Disconnect,
                 beginOperation: ( asyncCallback ) => BeginDisconnect( reuseSocket, asyncCallback, state: this ),
                 timeout,
                 cancellationToken
             );

        public Task DsDisconnectAsync( 
            IoBehavior ioBehavior, 
            Boolean reuseSocket,
            TimeSpan timeout, 
            CancellationToken cancellationToken = default 
        ){
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                return DsDisconnectAsync( reuseSocket, timeout, cancellationToken );
            }
            else if ( ioBehavior == IoBehavior.Synchronous )
            {
                DsDisconnect( reuseSocket, timeout, cancellationToken );
                return Task.CompletedTask;
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} = {default( IoBehavior )}" );
            }
        }

        public Task DsDisconnectAsync( 
            Boolean reuseSocket, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default 
        ) => Task.Run( () => DsDisconnect( reuseSocket, timeout, cancellationToken ) );

        //ValueTask is better, because we have to create every
        //time another result object, but Task instance is much
        //more larger and cannot be cached
        public ValueTask<Byte[]> DsReceiveAsync( 
            IoBehavior ioBehavior, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default 
        ){
            ValueTask<Byte[]> bytesOfResponse;
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                bytesOfResponse = new ValueTask<Byte[]>( DsReceiveAsync( timeout, cancellationToken ) );
            }
            else if ( ioBehavior == IoBehavior.Synchronous )
            {
                bytesOfResponse = new ValueTask<Byte[]>( DsReceive( timeout, cancellationToken ) );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }

            return bytesOfResponse;
        }

        public Task<Byte[]> DsReceiveAsync( TimeSpan timeout, CancellationToken cancellationToken = default ) =>
            Task.Run( () => DsReceive( timeout, cancellationToken ) );

        public Byte[] DsReceive( TimeSpan timeout, CancellationToken cancellationToken = default )
        {
            var cancelSource = CancellationTokenSource.CreateLinkedTokenSource( cancellationToken );

            try
            {
                cancelSource.CancelAfter( timeout );

                State = SocketState.Reading;

                Byte[] readBytes = this.ReadMessageBytes(
                    DsConstants.MAX_CHUNK_READ_PER_ONE_TIME,
                    DsConstants.MAX_AVAILABLE_READ_BYTES,
                    cancelSource.Token
                );

                State = SocketState.AlreadyRead;

                if ( readBytes.Length == 0 )
                {
                    DsLoggerSet.DefaultLogger.LogFatal( message: $"Read 0 bytes" );
                }

                return readBytes;
            }
            finally
            {
                cancelSource.Dispose();
            }
        }

        public void DsSend( 
            Byte[] bytesToSend, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default 
        ){
            SocketOperation( 
                SocketAsyncOperation.Send, 
                ( asyncCallback ) => BeginSend(
                    bytesToSend,
                    offset: 0,
                    bytesToSend.Length,
                    SocketFlags.None,
                    asyncCallback,
                    state: this
                ), 
                timeout, 
                cancellationToken 
            );
        }

        public Task DsSendAsync( 
            Byte[] bytesToSend, 
            TimeSpan timeoutToSend, 
            IoBehavior ioBehavior, 
            CancellationToken cancellationToken = default 
        ){
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                return DsSendAsync( bytesToSend, timeoutToSend, cancellationToken );
            }
            else if ( ioBehavior == IoBehavior.Synchronous )
            {
                DsSend( bytesToSend, timeoutToSend, cancellationToken );
                return Task.CompletedTask;
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }
        }

        public Task DsSendAsync( 
            Byte[] bytesToSend, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default 
        ) => Task.Run( () => DsSend( bytesToSend, timeout, cancellationToken ) );

        public void SocketOperation( 
            SocketAsyncOperation socketOp, 
            Func<AsyncCallback, IAsyncResult> beginOperation, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default 
        ){
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
                    DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Start disconnection" );

                    break;
                }

                default:
                {
                    throw new NotImplementedException( message: $"{socketOp} isn't supported" );
                }
            }

            var operationDone = new AsyncAutoResetEvent( set: false );

            var socketException = new SocketException( (Int32)SocketError.Success );
            ObjectDisposedException disposedException = null;

            AsyncCallback asyncCallback = ( asyncResult ) => SocketOperationCallback( 
                socketOp, 
                asyncResult, 
                operationDone, 
                out disposedException, 
                out socketException 
            );

            beginOperation( asyncCallback );
            WaitResultOfAsyncSocketOperation( 
                socketOp, 
                operationDone, 
                timeout, 
                cancellationToken, 
                ref socketException, 
                ref disposedException 
            );
        }

        //It throws exceptions, not <see cref="Boolean"/> value, because if socket isn't connected we should immediately end method where VerifyConnected is call
        public void VerifyConnected()
        {
            if ( State >= SocketState.Closing )
            {
                throw new ObjectDisposedException( nameof( AsyncSocket ) );
            }
            else if ( !Connected || ( ( SocketState.Disconnected <= State ) && ( State < SocketState.Closing ) ) )
            {
                throw new SocketException( (Int32)SocketError.NotConnected );
            }
        }

        protected override void Dispose( Boolean disposing )
        {
            if ( disposing && ( State < SocketState.Closing ) )
            {
                State = SocketState.Closing;

                base.Dispose( disposing );

                State = SocketState.Closed;
            }
        }

        protected void VerifyWorkState()
        {
            if ( ( SocketState.Failed <= State ) && ( State <= SocketState.Closed ) )
            {
                //Wanted to use idle socket
                throw new SocketException( (Int32)SocketError.SocketError );
            }
        }

        private void AcceptCallback( IAsyncResult asyncResult )
        {
            //Get the socket that handles the client request
            var stateAccept = (StateObjectForAccept)asyncResult.AsyncState;
            stateAccept.AcceptedSocket = stateAccept.Listener.EndAccept( asyncResult );
            State = SocketState.Accepted;

            //another thread can receive timeout and it will close stateAccept.AcceptDone
            if ( !stateAccept.AcceptDone.SafeWaitHandle.IsClosed )
            {
                //Signal that the connection has been made
                stateAccept.AcceptDone.Set();
            }
        }

        private void HandleExceptionOfEndSocketOp<T>( 
            Exception inEx, 
            AsyncAutoResetEvent operationDone, 
            out T outEx 
        ) where T : Exception
        {
            outEx = null;
            Interlocked.Exchange( ref outEx, inEx as T );
            State = SocketState.Failed;

            operationDone.Set();
        }

        //Now it is only for Connect, Send and Disconnect 
        private void SocketOperationCallback( 
            SocketAsyncOperation socketOp, 
            IAsyncResult asyncResult, 
            AsyncAutoResetEvent operationDone, 
            out ObjectDisposedException disposedException, 
            out SocketException socketException 
        ){
            var socket = (Socket)asyncResult.AsyncState;
            socketException = new SocketException( (Int32)SocketError.Success );
            disposedException = null;

            try
            {
                switch ( socketOp )
                {
                    case SocketAsyncOperation.Connect:
                    {
                        socket.EndConnect( asyncResult );
                        DsLoggerSet.DefaultLogger.LogInfo( $"Connection successfully made" );

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
                        DsLoggerSet.DefaultLogger.LogInfo( $"Disconnection successfully made" );

                        State = SocketState.Disconnected;

                        break;
                    }

                    default:
                    {
                        throw new NotImplementedException( message: $"{socketOp} doesn't supported" );
                    }
                }

                operationDone.Set();
            }
            catch ( SocketException ex )
            {
                HandleExceptionOfEndSocketOp( ex, operationDone, out socketException );
            }
            catch ( InvalidOperationException ex )
            {
                HandleExceptionOfEndSocketOp( ex, operationDone, out disposedException );
            }
        }

        /// <param name="socketException">
        /// If it is not ref parameter, it will not be updated by another thread
        /// </param>
        private void WaitResultOfAsyncSocketOperation( 
            SocketAsyncOperation socketOp, 
            AsyncAutoResetEvent operationDone, 
            TimeSpan timeout, 
            CancellationToken cancellationToken, 
            ref SocketException socketException, 
            ref ObjectDisposedException disposedException 
        ){
            Boolean isInTime = operationDone.Wait( timeout, cancellationToken );

            Exception exception;

            if ( ( socketException.SocketErrorCode == SocketError.Success ) && isInTime )
            {
                return;
            }
            else if ( socketException.SocketErrorCode != SocketError.Success )
            {
                exception = socketException;
            }
            else if ( !isInTime )
            {
                exception = new TimeoutException( message: $"Timeout during {socketOp} occured" );
            }
            else if ( disposedException != null )
            {
                exception = disposedException;
            }
            else
            {
                //it can't be
                exception = new InvalidProgramException();
            }

            throw exception;
        }
    }
}